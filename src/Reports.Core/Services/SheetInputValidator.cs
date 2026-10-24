using System.Text.RegularExpressions;

namespace Reports.Core.Services;

/// <summary>Limits applied to what one sync reads from the sheet.</summary>
public sealed record SyncLimits(int MaxRows = 5000, int MaxCellLength = 500);

/// <summary>Checks for untrusted sheet data (SEC-08).</summary>
public static class SheetInputValidator
{
    /// <summary>Maximum length of names and settlements.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Maximum length of free text.</summary>
    public const int MaxTextLength = 500;

    /// <summary>Largest accepted UAV count.</summary>
    public const int MaxUavCount = 999;

    private static readonly DateTime EarliestDate = new(2000, 1, 1);

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SpreadsheetIdPattern = new(
        @"^[A-Za-z0-9_-]{20,100}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>True for a plausible email address up to 254 characters.</summary>
    public static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 254 && EmailPattern.IsMatch(value.Trim());

    /// <summary>True for a Google spreadsheet ID.</summary>
    public static bool IsValidSpreadsheetId(string? value) =>
        value is not null && SpreadsheetIdPattern.IsMatch(value);

    /// <summary>Cuts the text to the limit; null becomes an empty string.</summary>
    public static string Clip(string? value, int maxLength, out bool clipped)
    {
        var text = value ?? string.Empty;
        clipped = text.Length > maxLength;
        return clipped ? text[..maxLength] : text;
    }

    /// <summary>Clamps a UAV count into 0..999 and says whether it was out of the upper range.</summary>
    public static int ClampUavCount(int value, out bool exceeded)
    {
        exceeded = value > MaxUavCount;
        return Math.Clamp(value, 0, MaxUavCount);
    }

    /// <summary>True when the date is not older than 2000 and not later than tomorrow.</summary>
    public static bool IsPlausibleDate(DateTime value, DateTime today) =>
        value.Date >= EarliestDate && value.Date <= today.Date.AddDays(1);
}
