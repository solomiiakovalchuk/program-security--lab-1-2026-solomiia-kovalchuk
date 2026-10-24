namespace Reports.Core.Aggregation;

/// <summary>
/// Теги шаблону документа. Скрипт нічого не знає про оформлення —
/// він лише замінює ці рядки на обчислені значення.
/// </summary>
public static class ReportTags
{
    public const string DateFrom = "{{ДАТА_З}}";
    public const string DateTo = "{{ДАТА_ПО}}";
    public const string UavWithCount = "{{ТИП_ТА_КІЛЬКІСТЬ_БПЛА}}";
    public const string Munitions = "{{БОЄПРИПАСИ}}";
    public const string TargetTypes = "{{ТИП_ЦІЛІ}}";
    public const string Subunit = "{{ПІДРОЗДІЛ}}";
    public const string Settlements = "{{НАСЕЛЕНИЙ_ПУНКТ}}";
    public const string Regions = "{{ОБЛАСТЬ}}";
    public const string Order = "{{НАКАЗ}}";
    public const string Position = "{{ПОСАДА}}";
    public const string Rank = "{{ЗВАННЯ}}";
    public const string SignerName = "{{ПІБ}}";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        DateFrom, DateTo, UavWithCount, Munitions, TargetTypes,
        Subunit, Settlements, Regions, Order, Position, Rank, SignerName
    };
}
