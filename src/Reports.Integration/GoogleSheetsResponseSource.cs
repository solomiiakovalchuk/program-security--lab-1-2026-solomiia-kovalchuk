using System.Globalization;
using Google.Apis.Sheets.v4;
using Reports.Core.Abstractions;
using Reports.Core.Domain;
using Reports.Core.Services;

namespace Reports.Integration;

/// <summary>
/// Читає довідники, людей і відповіді форми з Google-таблиці.
/// <para>
/// Колонки аркуша «Відповіді» шукаються <b>за заголовками</b>, а не за
/// фіксованими номерами: питання у формі можна переставити або додати,
/// і синхронізація не зламається.
/// </para>
/// </summary>
public sealed class GoogleSheetsResponseSource : IResponseSource
{
    private readonly IGoogleServiceFactory _authenticator;
    private readonly AppSettings _settings;
    private readonly SyncLimits _limits;

    public GoogleSheetsResponseSource(
        IGoogleServiceFactory authenticator, AppSettings settings, SyncLimits? limits = null)
    {
        _authenticator = authenticator;
        _settings = settings;
        _limits = limits ?? new SyncLimits();
    }

    /// <summary>Last row to read: header plus one row over the limit, so truncation can be detected.</summary>
    private int LastResponseRow => _limits.MaxRows + 2;

    public string Title => "Google Таблиця";

    public async Task<RawDirectories> LoadDirectoriesAsync(CancellationToken ct = default)
    {
        using var service = await CreateServiceAsync(ct);

        var directories = await ReadRangeAsync(service, SheetRange.Of(_settings.DirectoriesSheet, "A1:K200"), ct);
        var people = await ReadRangeAsync(service, SheetRange.Of(_settings.PeopleSheet, "A1:D200"), ct);

        return new RawDirectories
        {
            // Чотири блоки довідників лежать поруч: A|B, D|E, G|H, J|K, дані з рядка 3.
            Uav = ReadPairs(directories, leftColumn: 0),
            Targets = ReadPairs(directories, leftColumn: 3),
            Munitions = ReadPairs(directories, leftColumn: 6),
            Settlements = ReadPairs(directories, leftColumn: 9),
            People = ReadPeople(people)
        };
    }

    public async Task<IReadOnlyList<RawResponseRow>> LoadResponsesAsync(CancellationToken ct = default)
    {
        using var service = await CreateServiceAsync(ct);

        var rows = await ReadRangeAsync(service, SheetRange.Of(_settings.ResponsesSheet, $"A1:Z{LastResponseRow}"), ct);
        if (rows.Count < 2) return Array.Empty<RawResponseRow>();

        var map = ResponseColumnMap.FromHeader(rows[0]);
        var result = new List<RawResponseRow>(rows.Count - 1);

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count == 0) continue;

            var submitted = ParseDate(map.Value(row, map.Timestamp)) ?? DateTime.Today;

            result.Add(new RawResponseRow
            {
                // Номер рядка робить ключ стабільним навіть за однакових позначок часу.
                RowKey = $"{map.Value(row, map.Timestamp)}|{i + 1}",
                SubmittedAt = submitted,
                Date = ParseDate(map.Value(row, map.Date)),
                OperatorEmail = map.Value(row, map.Email),
                SettlementName = map.Value(row, map.Settlement),
                UavTypeName = map.Value(row, map.UavType),
                TargetTypeName = map.Value(row, map.TargetType),
                UavCount = ParseCount(map.Value(row, map.UavCount)),
                MunitionNames = map.Munitions
                                   .Select(c => map.Value(row, c))
                                   .Where(v => !string.IsNullOrWhiteSpace(v))
                                   .ToList(),
                StatusMarker = map.Value(row, map.Status)
            });
        }

        return result;
    }

    /// <summary>
    /// Назви всіх аркушів таблиці. Потрібні для перевірки зʼєднання:
    /// Google називає аркуш відповідей «Відповіді форми 1», а не «Відповіді»,
    /// і без цієї перевірки розбіжність виявляється лише порожньою синхронізацією.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListSheetsAsync(CancellationToken ct = default)
    {
        using var service = await CreateServiceAsync(ct);

        try
        {
            var meta = await service.Spreadsheets.Get(_settings.SpreadsheetId).ExecuteAsync(ct);
            return meta.Sheets
                       .Select(s => s.Properties?.Title ?? string.Empty)
                       .Where(t => t.Length > 0)
                       .ToList();
        }
        catch (Google.GoogleApiException ex)
        {
            throw new GoogleAccessException(DescribeApiError(ex, "відкрити таблицю"), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not GoogleAccessException)
        {
            throw new GoogleAccessException(
                "Таблицю не вдалося відкрити: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Підбирає назву аркуша зі списку за набором можливих початків назви.
    /// Повертає null, якщо нічого схожого немає.
    /// </summary>
    public static string? MatchSheet(IEnumerable<string> titles, params string[] prefixes)
    {
        var list = titles.ToList();

        foreach (var prefix in prefixes)
        {
            var exact = list.FirstOrDefault(t =>
                string.Equals(t, prefix, StringComparison.CurrentCultureIgnoreCase));
            if (exact is not null) return exact;
        }

        foreach (var prefix in prefixes)
        {
            var partial = list.FirstOrDefault(t =>
                t.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
            if (partial is not null) return partial;
        }

        return null;
    }

    /// <summary>
    /// Результат перевірки зʼєднання: що знайдено в таблиці і чого бракує.
    /// </summary>
    public sealed class Diagnosis
    {
        public required IReadOnlyList<string> SheetTitles { get; init; }
        public string? ResponsesSheet { get; init; }
        public string? DirectoriesSheet { get; init; }
        public string? PeopleSheet { get; init; }
        public int ResponseRows { get; init; }
        public IReadOnlyList<string> MatchedFields { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

        public bool IsUsable => ResponsesSheet is not null && MissingFields.Count == 0;
    }

    /// <summary>
    /// Перевіряє зʼєднання: відкриває таблицю, підбирає аркуші за назвами
    /// й зіставляє заголовки колонок форми з полями моделі.
    /// </summary>
    public async Task<Diagnosis> DiagnoseAsync(CancellationToken ct = default)
    {
        var titles = await ListSheetsAsync(ct);

        var responses = MatchSheet(titles, _settings.ResponsesSheet, "Відповіді", "Form Responses");
        var directories = MatchSheet(titles, _settings.DirectoriesSheet, "Довідники", "Довідник");
        var people = MatchSheet(titles, _settings.PeopleSheet, "Люди");

        if (responses is null)
            return new Diagnosis { SheetTitles = titles };

        using var service = await CreateServiceAsync(ct);
        var rows = await ReadRangeAsync(service, SheetRange.Of(responses, $"A1:Z{LastResponseRow}"), ct);

        if (rows.Count == 0)
            return new Diagnosis
            {
                SheetTitles = titles,
                ResponsesSheet = responses,
                DirectoriesSheet = directories,
                PeopleSheet = people,
                MissingFields = new[] { "аркуш відповідей порожній" }
            };

        var map = ResponseColumnMap.FromHeader(rows[0]);

        var matched = new List<string>();
        void Note(string name, int column)
        {
            if (column >= 0) matched.Add($"{name} → колонка «{rows[0][column]}»");
        }

        Note("дата", map.Date);
        Note("оператор", map.Email);
        Note("населений пункт", map.Settlement);
        Note("тип БпЛА", map.UavType);
        Note("тип цілі", map.TargetType);
        Note("кількість", map.UavCount);
        Note("статус", map.Status);
        foreach (var column in map.Munitions)
            matched.Add($"боєприпас → колонка «{rows[0][column]}»");

        return new Diagnosis
        {
            SheetTitles = titles,
            ResponsesSheet = responses,
            DirectoriesSheet = directories,
            PeopleSheet = people,
            ResponseRows = Math.Max(0, rows.Count - 1),
            MatchedFields = matched,
            MissingFields = map.MissingFields()
        };
    }

    /// <summary>
    /// Витягує ідентифікатор із того, що вставив користувач. Найчастіша помилка —
    /// скопійована адреса цілком; тоді Google повертає 404, і причина неочевидна.
    /// </summary>
    public static string NormalizeSpreadsheetId(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0) return value;

        var match = System.Text.RegularExpressions.Regex.Match(
            value, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
        if (match.Success) return match.Groups[1].Value;

        // Просто адреса без /d/ — віддаємо як є, хай Google скаже своє.
        return value;
    }

    /// <summary>
    /// Перекладає відповідь Google API у пораду, що саме робити.
    /// Код відповіді тут головний: 403 і 404 лікуються по-різному.
    /// </summary>
    private string DescribeApiError(Google.GoogleApiException ex, string action)
    {
        var detail = ex.Error?.Message ?? ex.Message;
        var id = _settings.SpreadsheetId;
        var br = Environment.NewLine + Environment.NewLine;

        var advice = (int)ex.HttpStatusCode switch
        {
            404 =>
                "Таблицю з таким ідентифікатором не знайдено." + br +
                $"Зараз указано: «{id}»" + br +
                "Потрібен лише ідентифікатор із адреси, а не вся адреса:" + Environment.NewLine +
                "docs.google.com/spreadsheets/d/[ЦЕЙ_РЯДОК]/edit",

            403 when detail.Contains("has not been used in project", StringComparison.OrdinalIgnoreCase)
                  || detail.Contains("is disabled", StringComparison.OrdinalIgnoreCase) =>
                "У проєкті Google Cloud не увімкнено Google Sheets API." + br +
                "APIs & Services → Library → Google Sheets API → Enable." + Environment.NewLine +
                "Після ввімкнення зачекайте одну-дві хвилини.",

            403 =>
                "Доступ заборонено: акаунт, яким ви ввійшли, не має доступу до цієї таблиці." + br +
                "Перевірте, що авторизувалися тим самим акаунтом, якому належить таблиця, " +
                "або відкрийте до неї доступ. Змінити акаунт: «Забути токен» і повторити.",

            401 =>
                "Термін дії дозволу минув або його відкликано." + br +
                "Натисніть «Забути токен» і повторіть перевірку.",

            400 =>
                "Google не зрозумів запит — найчастіше через неправильну назву аркуша " +
                "або ідентифікатор.",

            _ => "Google повернув помилку."
        };

        return $"Не вдалося {action}." + br + advice + br +
               $"Відповідь Google ({(int)ex.HttpStatusCode}): {detail}";
    }

    private async Task<SheetsService> CreateServiceAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.SpreadsheetId))
            throw new GoogleAccessException(
                "Не вказано ідентифікатор Google-таблиці. Відкрийте «Налаштування» і вставте " +
                "його з адреси таблиці: docs.google.com/spreadsheets/d/<ІДЕНТИФІКАТОР>/edit");

        if (!SheetInputValidator.IsValidSpreadsheetId(_settings.SpreadsheetId))
            throw new GoogleAccessException(
                "Ідентифікатор Google-таблиці має неправильний формат: очікується 20-100 символів " +
                "(літери, цифри, «-» і «_»).");

        return await _authenticator.CreateServiceAsync(GoogleAccess.ReadOnly, ct);
    }

    private async Task<IList<IList<object>>> ReadRangeAsync(
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
                DescribeApiError(ex, $"прочитати діапазон «{range}»"), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not GoogleAccessException)
        {
            throw new GoogleAccessException(
                $"Не вдалося прочитати діапазон «{range}»: {ex.Message}", ex);
        }
    }

    /// <summary>Читає блок довідника з двох сусідніх колонок, починаючи з рядка 3.</summary>
    private static IReadOnlyList<(string, string)> ReadPairs(IList<IList<object>> rows, int leftColumn)
    {
        var pairs = new List<(string, string)>();

        for (int i = 2; i < rows.Count; i++)
        {
            var left = Cell(rows[i], leftColumn);
            if (string.IsNullOrWhiteSpace(left)) continue;

            pairs.Add((
                SheetInputValidator.Clip(left.Trim(), SheetInputValidator.MaxNameLength, out _),
                SheetInputValidator.Clip(
                    Cell(rows[i], leftColumn + 1).Trim(), SheetInputValidator.MaxNameLength, out _)));
        }

        return pairs;
    }

    private static IReadOnlyList<Person> ReadPeople(IList<IList<object>> rows)
    {
        var people = new List<Person>();

        for (int i = 1; i < rows.Count; i++)
        {
            var email = Cell(rows[i], 0).Trim();
            var name = Cell(rows[i], 1).Trim();
            if (email.Length == 0 && name.Length == 0) continue;

            // Sheet data is untrusted: a malformed email is skipped, long text is capped.
            if (email.Length > 0 && !SheetInputValidator.IsValidEmail(email)) continue;

            string Cap(string v, int max) => SheetInputValidator.Clip(v, max, out _);

            people.Add(new Person
            {
                Email = email,
                FullName = Cap(name, SheetInputValidator.MaxNameLength),
                Rank = Cap(Cell(rows[i], 2).Trim(), SheetInputValidator.MaxNameLength),
                Position = Cap(Cell(rows[i], 3).Trim(), SheetInputValidator.MaxTextLength)
            });
        }

        return people;
    }

    private static string Cell(IList<object> row, int index) =>
        index >= 0 && index < row.Count ? row[index]?.ToString() ?? string.Empty : string.Empty;

    private static DateTime? ParseDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string[] formats =
        {
            "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "MM/dd/yyyy HH:mm:ss", "M/d/yyyy"
        };

        if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out var exact))
            return exact;

        if (DateTime.TryParse(raw.Trim(), CultureInfo.CurrentCulture,
                              DateTimeStyles.None, out var current))
            return current;

        // Google іноді віддає дату як серійне число Excel.
        if (double.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)
            && serial > 20000 && serial < 80000)
            return new DateTime(1899, 12, 30).AddDays(serial);

        return null;
    }

    private static int ParseCount(string raw)
    {
        if (int.TryParse(raw?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return Math.Max(0, n);
        return 0;
    }
}
