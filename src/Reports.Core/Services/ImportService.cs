using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>Підсумок синхронізації для показу користувачеві.</summary>
public sealed record ImportResult(
    int TotalRows,
    int Imported,
    int Skipped,
    int NeedsAttention,
    IReadOnlyList<string> UnknownValues,
    string? TruncationNote = null)
{
    /// <summary>True when rows or cells were cut by the sync limits.</summary>
    public bool Truncated => TruncationNote is not null;

    public string Describe()
    {
        if (TotalRows == 0)
            return "Джерело не повернуло жодного рядка.";

        var text = $"Прочитано рядків: {TotalRows}. Імпортовано: {Imported}. " +
                   $"Уже були в базі: {Skipped}.";

        if (NeedsAttention > 0)
            text += $" Потребують уваги: {NeedsAttention}.";

        if (UnknownValues.Count > 0)
            text += Environment.NewLine +
                    "Немає в довідниках: " + string.Join(", ", UnknownValues);

        if (TruncationNote is not null)
            text += Environment.NewLine + TruncationNote;

        return text;
    }
}

/// <summary>
/// Переносить сирі рядки відповідей у доменну модель: зіставляє назви
/// з довідниками, обчислює дату, відсіює вже імпортовані подачі.
/// </summary>
public sealed class ImportService
{
    private readonly IMissionRepository _missions;
    private readonly IReferenceRepository _references;

    private readonly SyncLimits _limits;
    private readonly AccessGuard? _guard;
    private readonly IAuditSink _audit;

    public ImportService(
        IMissionRepository missions, IReferenceRepository references, SyncLimits? limits = null,
        AccessGuard? guard = null, IAuditSink? audit = null)
    {
        _missions = missions;
        _references = references;
        _limits = limits ?? new SyncLimits();
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <summary>Imports new rows; needs the Sync action when a guard is configured, and records Sync.Import.</summary>
    public async Task<ImportResult> ImportAsync(IResponseSource source, CancellationToken ct = default)
    {
        _guard?.Demand(AccessActions.Sync);

        try
        {
            var result = await ImportCoreAsync(source, ct);
            _audit.Record(AuditAction.SyncImport, AuditTarget.GoogleSync, null, AuditOutcome.Success,
                new Dictionary<string, string>
                {
                    ["added"] = result.Imported.ToString(),
                    ["updated"] = "0",
                    ["skipped"] = result.Skipped.ToString(),
                    ["total"] = result.TotalRows.ToString()
                });
            return result;
        }
        catch (Exception)
        {
            _audit.Record(AuditAction.SyncImport, AuditTarget.GoogleSync, null, AuditOutcome.Failure);
            throw;
        }
    }

    private async Task<ImportResult> ImportCoreAsync(IResponseSource source, CancellationToken ct)
    {
        var loaded = await source.LoadResponsesAsync(ct);
        ct.ThrowIfCancellationRequested();

        var rowsCut = loaded.Count > _limits.MaxRows;
        var rows = rowsCut ? loaded.Take(_limits.MaxRows).ToList() : loaded;
        var cellsCut = false;

        var known = await _missions.GetImportedKeysAsync(ct);

        var uav = Index(await _references.GetUavTypesAsync(ct));
        var targets = Index(await _references.GetTargetTypesAsync(ct));
        var munitions = Index(await _references.GetMunitionTypesAsync(ct));
        var settlements = Index(await _references.GetSettlementsAsync(ct));

        var fresh = new List<Mission>();
        var unknown = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        int skipped = 0, needsAttention = 0;

        foreach (var original in rows)
        {
            ct.ThrowIfCancellationRequested();

            var row = Sanitize(original, out var suspicious, ref cellsCut);

            if (known.Contains(row.RowKey))
            {
                skipped++;
                continue;
            }

            var uavType = Lookup(uav, row.UavTypeName, unknown);
            var targetType = Lookup(targets, row.TargetTypeName, unknown);
            var settlement = Lookup(settlements, row.SettlementName, unknown);

            var status = RecordStatusExtensions.Parse(row.StatusMarker);
            if (status == RecordStatus.Active && (uavType is null || targetType is null || suspicious))
            {
                status = RecordStatus.NeedsAttention;
                needsAttention++;
            }

            var engagement = new TargetEngagement
            {
                UavTypeId = uavType?.Id,
                TargetTypeId = targetType?.Id,
                UavCount = row.UavCount
            };

            foreach (var name in row.MunitionNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var munition = Lookup(munitions, name, unknown);
                if (munition is not null)
                    engagement.Munitions.Add(new MunitionUse { MunitionTypeId = munition.Id });
            }

            fresh.Add(new Mission
            {
                SourceRowKey = row.RowKey,
                // Порожнє поле «Дата» у формі означає день подання.
                Date = (row.Date ?? row.SubmittedAt).Date,
                SubmittedAt = row.SubmittedAt,
                OperatorEmail = row.OperatorEmail.Trim(),
                SettlementId = settlement?.Id,
                Status = status,
                Targets = { engagement }
            });
        }

        ct.ThrowIfCancellationRequested();

        if (fresh.Count > 0)
            await _missions.AddRangeAsync(fresh, ct);

        return new ImportResult(
            rows.Count, fresh.Count, skipped, needsAttention, unknown.ToList(),
            DescribeTruncation(rowsCut, loaded.Count, cellsCut));
    }

    private string? DescribeTruncation(bool rowsCut, int loadedCount, bool cellsCut)
    {
        if (!rowsCut && !cellsCut) return null;

        var parts = new List<string>();
        if (rowsCut)
            parts.Add($"прочитано лише перші {_limits.MaxRows} рядків із {loadedCount}");
        if (cellsCut)
            parts.Add($"довгі значення обрізано до {_limits.MaxCellLength} символів");
        return "Дані обрізано: " + string.Join("; ", parts) + ".";
    }

    /// <summary>
    /// Sheet data is untrusted (SEC-08, SEC-15): caps lengths, drops a bad email or date,
    /// clamps the UAV count. <paramref name="suspicious"/> is set when the row needs a human look.
    /// </summary>
    private RawResponseRow Sanitize(RawResponseRow row, out bool suspicious, ref bool cellsCut)
    {
        var bad = false;
        var cut = false;

        string Cell(string? value, int cap)
        {
            var text = SheetInputValidator.Clip(value, _limits.MaxCellLength, out var c1);
            text = SheetInputValidator.Clip(text, cap, out var c2);
            if (c1) cut = true;
            return text;
        }

        var email = Cell(row.OperatorEmail, SheetInputValidator.MaxTextLength).Trim();
        if (email.Length > 0 && !SheetInputValidator.IsValidEmail(email))
        {
            email = string.Empty;
            bad = true;
        }

        var count = SheetInputValidator.ClampUavCount(row.UavCount, out var exceeded);
        if (exceeded) bad = true;

        var today = DateTime.Today;
        var submitted = row.SubmittedAt;
        if (!SheetInputValidator.IsPlausibleDate(submitted, today))
        {
            submitted = today;
            bad = true;
        }

        var date = row.Date;
        if (date is not null && !SheetInputValidator.IsPlausibleDate(date.Value, today))
        {
            date = null;
            bad = true;
        }

        var sanitized = new RawResponseRow
        {
            RowKey = row.RowKey,
            SubmittedAt = submitted,
            Date = date,
            OperatorEmail = email,
            SettlementName = Cell(row.SettlementName, SheetInputValidator.MaxNameLength),
            UavTypeName = Cell(row.UavTypeName, SheetInputValidator.MaxNameLength),
            TargetTypeName = Cell(row.TargetTypeName, SheetInputValidator.MaxNameLength),
            UavCount = count,
            MunitionNames = row.MunitionNames
                .Select(n => Cell(n, SheetInputValidator.MaxNameLength))
                .ToList(),
            StatusMarker = Cell(row.StatusMarker, SheetInputValidator.MaxNameLength)
        };

        if (cut) cellsCut = true;
        suspicious = bad;
        return sanitized;
    }

    private static Dictionary<string, T> Index<T>(IEnumerable<T> items) where T : ReferenceItem =>
        items.GroupBy(i => i.ShortName.Trim(), StringComparer.CurrentCultureIgnoreCase)
             .ToDictionary(g => g.Key, g => g.First(), StringComparer.CurrentCultureIgnoreCase);

    private static T? Lookup<T>(Dictionary<string, T> index, string name, ISet<string> unknown)
        where T : ReferenceItem
    {
        var key = name?.Trim() ?? string.Empty;
        if (key.Length == 0) return null;

        if (index.TryGetValue(key, out var found)) return found;

        unknown.Add(key);
        return null;
    }
}
