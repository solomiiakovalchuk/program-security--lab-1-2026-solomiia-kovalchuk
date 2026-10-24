using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>
/// Synchronization for the Sync action: tops up the local reference data from the source
/// (system work with the unguarded repositories, so an Operator can sync) and imports the responses.
/// </summary>
public sealed class SyncService
{
    private readonly ImportService _import;
    private readonly IReferenceRepository _references;
    private readonly IPersonRepository _people;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;

    /// <summary>Creates the service; the repositories must be the unguarded ones.</summary>
    public SyncService(
        ImportService import,
        IReferenceRepository references,
        IPersonRepository people,
        AccessGuard guard,
        IAuditSink? audit = null)
    {
        _import = import;
        _references = references;
        _people = people;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <summary>Syncs from the source; <paramref name="external"/> is true for Google, which also records Google.SyncCompleted.</summary>
    public async Task<ImportResult> SyncAsync(IResponseSource source, bool external, CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.Sync);

        try
        {
            await MergeDirectoriesAsync(source, ct);
            var result = await _import.ImportAsync(source, ct);

            if (external)
            {
                _audit.Record(AuditAction.GoogleSyncCompleted, AuditTarget.GoogleSync, null, AuditOutcome.Success,
                    new Dictionary<string, string>
                    {
                        ["added"] = result.Imported.ToString(),
                        ["updated"] = "0",
                        ["skipped"] = result.Skipped.ToString(),
                        ["total"] = result.TotalRows.ToString()
                    });
            }

            return result;
        }
        catch (Exception)
        {
            if (external)
            {
                _audit.Record(AuditAction.GoogleSyncCompleted, AuditTarget.GoogleSync, null, AuditOutcome.Failure);
            }

            throw;
        }
    }

    /// <summary>Directories from the source are added to the base; existing records are not touched.</summary>
    private async Task MergeDirectoriesAsync(IResponseSource source, CancellationToken ct)
    {
        var raw = await source.LoadDirectoriesAsync(ct);

        await MergeAsync(raw.Uav, await _references.GetUavTypesAsync(ct),
            (s, f) => new UavType { ShortName = s, FullName = f }, ct);
        await MergeAsync(raw.Targets, await _references.GetTargetTypesAsync(ct),
            (s, f) => new TargetType { ShortName = s, FullName = f }, ct);
        await MergeAsync(raw.Munitions, await _references.GetMunitionTypesAsync(ct),
            (s, f) => new MunitionType { ShortName = s, FullName = f }, ct);
        await MergeAsync(raw.Settlements, await _references.GetSettlementsAsync(ct),
            (s, region) => new Settlement { ShortName = s, Region = region }, ct);

        var existingPeople = await _people.GetAllAsync(ct);
        foreach (var person in raw.People)
        {
            if (existingPeople.Any(p => string.Equals(p.Email, person.Email, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            await _people.SaveAsync(person, ct);
        }
    }

    private async Task MergeAsync<T>(
        IReadOnlyList<(string Left, string Right)> incoming,
        IReadOnlyList<T> existing,
        Func<string, string, T> create,
        CancellationToken ct) where T : ReferenceItem
    {
        foreach (var (left, right) in incoming)
        {
            if (string.IsNullOrWhiteSpace(left)) continue;
            if (existing.Any(e => string.Equals(e.ShortName, left, StringComparison.CurrentCultureIgnoreCase)))
                continue;

            await _references.SaveAsync(create(left, right), ct);
        }
    }
}
