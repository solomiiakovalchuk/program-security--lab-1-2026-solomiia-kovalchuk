using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Reports.Core.Abstractions;
using Reports.Core.Identity;
using Reports.Core.Services;

namespace Reports.Integration;

/// <summary>
/// Дописує у Google-таблицю те, що зʼявилося в застосунку: нові позиції
/// довідників, нових людей і зміни колонки «Статус».
/// <para>
/// Працює <b>тільки на додавання</b>: наявні рядки не переписуються й не
/// видаляються. Над тією самою таблицею працює ще й Apps Script, тому
/// затирати чужі правки застосунок не має права.
/// </para>
/// </summary>
public sealed class GoogleSheetsWriter : IResponseSink
{
    /// <summary>Ліві колонки чотирьох блоків аркуша «Довідники»: A, D, G, J.</summary>
    private const int UavColumn = 0;
    private const int TargetColumn = 3;
    private const int MunitionColumn = 6;
    private const int SettlementColumn = 9;

    /// <summary>Дані блоків довідників починаються з третього рядка.</summary>
    private const int DirectoryFirstRow = 3;

    /// <summary>Дані аркуша «Люди» починаються з другого рядка.</summary>
    private const int PeopleFirstRow = 2;

    private readonly IGoogleServiceFactory _authenticator;
    private readonly AppSettings _settings;
    private readonly SpreadsheetConfirmation _confirmation;
    private readonly AccessGuard? _guard;

    public GoogleSheetsWriter(
        IGoogleServiceFactory authenticator, AppSettings settings, SpreadsheetConfirmation confirmation,
        AccessGuard? guard = null)
    {
        _guard = guard;
        _authenticator = authenticator;
        _settings = settings;
        _confirmation = confirmation;
    }

    public string Title => "Google Таблиця";

    public async Task<PublishResult> PublishAsync(PublishPayload payload, CancellationToken ct = default)
    {
        _guard?.Demand(AccessActions.PublishToGoogle);

        if (!_settings.UploadEnabled)
            throw new GoogleAccessException(
                "Вивантаження в Google-таблицю вимкнене. Адміністратор може ввімкнути його в налаштуваннях.");

        if (string.IsNullOrWhiteSpace(_settings.SpreadsheetId))
            throw new GoogleAccessException(
                "Не вказано ідентифікатор Google-таблиці — вивантажувати нікуди.");

        if (!SheetInputValidator.IsValidSpreadsheetId(_settings.SpreadsheetId))
            throw new GoogleAccessException("Ідентифікатор Google-таблиці має неправильний формат.");

        if (!await _confirmation.EnsureConfirmedAsync(ct))
            return new PublishResult(0, 0, 0, new[] { "Вивантаження скасовано: таблицю не підтверджено." });

        using var service = await _authenticator.CreateServiceAsync(GoogleAccess.ReadWrite, ct);

        var updates = new List<ValueRange>();
        var notes = new List<string>();

        int directoriesAdded = await PlanDirectoriesAsync(service, payload, updates, ct);
        int peopleAdded = await PlanPeopleAsync(service, payload, updates, ct);
        int statusesUpdated = await PlanStatusesAsync(service, payload, updates, notes, ct);

        if (updates.Count == 0)
            return new PublishResult(0, 0, 0, notes);

        try
        {
            var body = BuildRequest(updates);

            await service.Spreadsheets.Values
                .BatchUpdate(body, _settings.SpreadsheetId)
                .ExecuteAsync(ct);
        }
        catch (Google.GoogleApiException ex) when ((int)ex.HttpStatusCode is 401 or 403)
        {
            throw new GoogleAccessException(
                "Немає права на запис у таблицю." + Environment.NewLine + Environment.NewLine +
                "Якщо ви давали згоду раніше, вона була лише на читання. Натисніть " +
                "«Забути токен», повторіть перевірку зʼєднання і дозвольте запис." +
                Environment.NewLine + Environment.NewLine +
                $"Відповідь Google ({(int)ex.HttpStatusCode}): {ex.Error?.Message ?? ex.Message}", ex);
        }

        return new PublishResult(directoriesAdded, peopleAdded, statusesUpdated, notes);
    }

    /// <summary>Builds the batch request: RAW input and every value neutralised against formula injection.</summary>
    internal static BatchUpdateValuesRequest BuildRequest(IEnumerable<ValueRange> updates) => new()
    {
        ValueInputOption = "RAW",
        Data = updates
            .Select(u => new ValueRange
            {
                Range = u.Range,
                Values = u.Values
                    .Select(row => (IList<object>)row.Select(v => SheetValueSanitizer.NeutralizeCell(v)).ToList())
                    .ToList()
            })
            .ToList()
    };

    /// <summary>Дописує в кожен блок довідників позиції, яких у ньому ще немає.</summary>
    private async Task<int> PlanDirectoriesAsync(
        SheetsService service, PublishPayload payload, List<ValueRange> updates, CancellationToken ct)
    {
        var rows = await ReadAsync(service, SheetRange.Of(_settings.DirectoriesSheet, "A1:K200"), ct);
        return PlanDirectories(rows, payload, updates);
    }

    internal int PlanDirectories(IList<IList<object>> rows, PublishPayload payload, List<ValueRange> updates)
    {
        int added = 0;
        added += PlanBlock(rows, UavColumn, payload.Uav, updates);
        added += PlanBlock(rows, TargetColumn, payload.Targets, updates);
        added += PlanBlock(rows, MunitionColumn, payload.Munitions, updates);
        added += PlanBlock(rows, SettlementColumn, payload.Settlements, updates);
        return added;
    }

    private int PlanBlock(
        IList<IList<object>> rows, int leftColumn,
        IReadOnlyList<DirectoryPair> items, List<ValueRange> updates)
    {
        var existing = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        int nextRow = DirectoryFirstRow;

        for (int i = DirectoryFirstRow - 1; i < rows.Count; i++)
        {
            var value = Cell(rows[i], leftColumn).Trim();
            if (value.Length == 0) continue;

            existing.Add(value);
            nextRow = i + 2;   // наступний рядок після останнього заповненого
        }

        var fresh = items
            .Where(p => p.Left.Length > 0 && !existing.Contains(p.Left)
                        && !existing.Contains(SheetValueSanitizer.Neutralize(p.Left)))
            .ToList();

        if (fresh.Count == 0) return 0;

        var from = $"{SheetRange.Column(leftColumn)}{nextRow}";
        var to = $"{SheetRange.Column(leftColumn + 1)}{nextRow + fresh.Count - 1}";

        updates.Add(new ValueRange
        {
            Range = SheetRange.Of(_settings.DirectoriesSheet, from + ":" + to),
            Values = fresh.Select(p => (IList<object>)new object[] { p.Left, p.Right }).ToList()
        });

        return fresh.Count;
    }

    /// <summary>Дописує людей, яких немає в аркуші «Люди» за адресою пошти.</summary>
    private async Task<int> PlanPeopleAsync(
        SheetsService service, PublishPayload payload, List<ValueRange> updates, CancellationToken ct)
    {
        var rows = await ReadAsync(service, SheetRange.Of(_settings.PeopleSheet, "A1:D200"), ct);
        return PlanPeople(rows, payload, updates);
    }

    internal int PlanPeople(IList<IList<object>> rows, PublishPayload payload, List<ValueRange> updates)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int nextRow = PeopleFirstRow;

        for (int i = PeopleFirstRow - 1; i < rows.Count; i++)
        {
            var email = Cell(rows[i], 0).Trim();
            if (email.Length == 0) continue;

            existing.Add(email);
            nextRow = i + 2;
        }

        var fresh = payload.People
            .Where(p => !existing.Contains(p.Email.Trim())
                        && !existing.Contains(SheetValueSanitizer.Neutralize(p.Email.Trim())))
            .ToList();
        if (fresh.Count == 0) return 0;

        updates.Add(new ValueRange
        {
            Range = SheetRange.Of(_settings.PeopleSheet, $"A{nextRow}:D{nextRow + fresh.Count - 1}"),
            Values = fresh
                .Select(p => (IList<object>)new object[] { p.Email, p.FullName, p.Rank, p.Position })
                .ToList()
        });

        return fresh.Count;
    }

    /// <summary>
    /// Проставляє колонку «Статус» там, де локальне значення відрізняється
    /// від того, що вже стоїть у таблиці. Якщо колонки немає — створює її.
    /// </summary>
    private async Task<int> PlanStatusesAsync(
        SheetsService service, PublishPayload payload,
        List<ValueRange> updates, List<string> notes, CancellationToken ct)
    {
        if (payload.Statuses.Count == 0) return 0;

        var rows = await ReadAsync(service, SheetRange.Of(_settings.ResponsesSheet, "A1:Z5000"), ct);
        return PlanStatuses(rows, payload, updates, notes);
    }

    internal int PlanStatuses(
        IList<IList<object>> rows, PublishPayload payload, List<ValueRange> updates, List<string> notes)
    {
        if (payload.Statuses.Count == 0) return 0;

        if (rows.Count == 0)
        {
            notes.Add("Аркуш відповідей порожній — статуси не записані.");
            return 0;
        }

        var map = ResponseColumnMap.FromHeader(rows[0]);
        int statusColumn = map.Status;

        if (statusColumn < 0)
        {
            // Колонки ще немає: створюємо її одразу за останньою наявною.
            statusColumn = rows.Max(r => r.Count);
            updates.Add(new ValueRange
            {
                Range = SheetRange.Cell(_settings.ResponsesSheet, statusColumn, 1),
                Values = new List<IList<object>> { new object[] { "Статус" } }
            });
            notes.Add($"У таблиці створено колонку «Статус» ({SheetRange.Column(statusColumn)}).");
        }

        int changed = 0;
        foreach (var change in payload.Statuses)
        {
            if (!TryGetRowNumber(change.SourceRowKey, out int rowNumber)) continue;
            if (rowNumber < 2 || rowNumber > rows.Count) continue;

            var current = Cell(rows[rowNumber - 1], statusColumn).Trim();
            var desired = change.Marker.Trim();
            if (string.Equals(current, desired, StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(current, SheetValueSanitizer.Neutralize(desired), StringComparison.CurrentCultureIgnoreCase))
                continue;

            updates.Add(new ValueRange
            {
                Range = SheetRange.Cell(_settings.ResponsesSheet, statusColumn, rowNumber),
                Values = new List<IList<object>> { new object[] { desired } }
            });
            changed++;
        }

        return changed;
    }

    /// <summary>
    /// Номер рядка джерела зашитий у ключі імпорту «позначка часу|номер рядка» —
    /// саме заради цього запису він там і зберігається.
    /// </summary>
    private static bool TryGetRowNumber(string sourceRowKey, out int rowNumber)
    {
        rowNumber = 0;
        var parts = (sourceRowKey ?? string.Empty).Split('|');
        return parts.Length == 2 && int.TryParse(parts[1], out rowNumber);
    }

    private async Task<IList<IList<object>>> ReadAsync(
        SheetsService service, string range, CancellationToken ct)
    {
        try
        {
            var response = await service.Spreadsheets.Values
                .Get(_settings.SpreadsheetId, range)
                .ExecuteAsync(ct);

            return response.Values ?? new List<IList<object>>();
        }
        catch (Google.GoogleApiException ex)
        {
            throw new GoogleAccessException(
                $"Не вдалося прочитати «{range}» перед вивантаженням: " +
                (ex.Error?.Message ?? ex.Message), ex);
        }
    }

    private static string Cell(IList<object> row, int index) =>
        index >= 0 && index < row.Count ? row[index]?.ToString() ?? string.Empty : string.Empty;
}
