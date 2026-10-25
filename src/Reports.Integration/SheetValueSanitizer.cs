namespace Reports.Integration;

/// <summary>Neutralises spreadsheet formula injection in values written to the sheet.</summary>
public static class SheetValueSanitizer
{
    /// <summary>Prefixes values that start with =, +, - or @ with an apostrophe; null becomes empty.</summary>
    public static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
    }

    /// <summary>Neutralises a cell value of any type; non-text values are converted to text first.</summary>
    public static object NeutralizeCell(object? value) => Neutralize(value?.ToString());
}
