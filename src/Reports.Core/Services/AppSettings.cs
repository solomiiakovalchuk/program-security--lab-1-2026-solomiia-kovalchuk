namespace Reports.Core.Services;

/// <summary>
/// Постійні реквізити частини. Аналог блоку CONFIG у скриптовій версії.
/// Гриф, адресат і резолюція вписуються у шаблон .docx при його створенні,
/// тому тут зберігаються лише для перебудови шаблону.
/// </summary>
public sealed class AppSettings
{
    public string Classification { get; set; } = "ВІДКРИТА ІНФОРМАЦІЯ";

    public string Addressee { get; set; } = "Командиру\nвійськової частини А0000";

    public string Resolution { get; set; } =
        "НС А та ППО\nПідготувати документи на списання встановленим порядком";

    public string ResolutionRank { get; set; } = "полковник";
    public string ResolutionName { get; set; } = "І. ПРІЗВИЩЕНКО";

    public string SubunitText { get; set; } =
        "взводом перехоплювачів безпілотних літальних апаратів зенітної батареї військової частини А0000";

    public string OrderDefault { get; set; } = "командира військової частини від 01.01.2026 №1т";

    public string DefaultSignerEmail { get; set; } = string.Empty;

    /// <summary>Ідентифікатор Google-таблиці «Рапорти БпЛА» (з адреси документа).</summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>Spreadsheet ID the administrator has confirmed as the write target; empty until confirmed.</summary>
    public string ConfirmedSpreadsheetId { get; set; } = string.Empty;

    /// <summary>Upload to the sheet is off by default; the write scope is requested only when true.</summary>
    public bool UploadEnabled { get; set; } = false;

    /// <summary>Назва аркуша з відповідями форми.</summary>
    public string ResponsesSheet { get; set; } = "Відповіді";

    public string DirectoriesSheet { get; set; } = "Довідники";
    public string PeopleSheet { get; set; } = "Люди";

    /// <summary>Тека, куди складаються сформовані рапорти.</summary>
    public string OutputFolder { get; set; } = string.Empty;

    /// <summary>Працювати на демонстраційних даних, не звертаючись до Google.</summary>
    public bool UseDemoData { get; set; } = true;

    public string FontName { get; set; } = "Times New Roman";
    public int FontSize { get; set; } = 14;
}
