using System.Text;
using System.Text.RegularExpressions;
using Reports.Core.Audit;

namespace Reports.Audit;

/// <summary>Builds the Details text from a per-action whitelist; secrets and unknown keys are dropped.</summary>
public static partial class AuditDetailsSanitizer
{
    /// <summary>Maximum length of the stored details.</summary>
    public const int MaxLength = 2000;

    private static readonly IReadOnlyDictionary<string, string[]> Whitelist = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [AuditAction.LoginFailed] = ["loginKnown", "reason", "attempts"],
        [AuditAction.AccountLockedOut] = ["attempts", "lockedUntil", "lockedUntilUtc"],
        [AuditAction.PasswordChanged] = ["initiator", "mustChange"],
        [AuditAction.PasswordResetByAdmin] = ["mustChange"],
        [AuditAction.AccountCreated] = ["role"],
        [AuditAction.AccountRoleChanged] = ["oldRole", "newRole"],
        [AuditAction.AccessDenied] = ["action", "role"],
        [AuditAction.ReportGenerated] = ["file", "period", "count"],
        [AuditAction.MissionCancelled] = ["reason"],
        [AuditAction.MissionRestored] = ["reason"],
        [AuditAction.SyncImport] = ["added", "updated", "skipped", "total"],
        [AuditAction.ReferenceAdded] = ["kind", "name"],
        [AuditAction.ReferenceUpdated] = ["kind", "name"],
        [AuditAction.ReferenceDeleted] = ["kind", "name"],
        [AuditAction.PersonAdded] = ["email"],
        [AuditAction.PersonUpdated] = ["email"],
        [AuditAction.PersonDeleted] = ["email"],
        [AuditAction.GoogleSyncCompleted] = ["added", "updated", "skipped", "total"],
        [AuditAction.GooglePublishCompleted] = ["rows", "file"],
        [AuditAction.GoogleConnectionChecked] = ["result"],
        [AuditAction.GoogleSpreadsheetChanged] = ["spreadsheetId", "fromId", "toId"],
        [AuditAction.SettingsChanged] = ["fields"],
        [AuditAction.TemplateRebuilt] = ["fields"],
        [AuditAction.AuditExported] = ["format", "scope", "from", "to", "count"],
        [AuditAction.AuditRetentionApplied] = ["cutoff", "deleted"],
        [AuditAction.ApplicationStarted] = ["version"],
        [AuditAction.IntegrityCheckFailed] = ["reason", "count"],
        [AuditAction.IntegrityUnsigned] = ["reason"],
    };

    /// <summary>Returns whether the action is a key of the <see cref="AuditAction"/> registry.</summary>
    public static bool IsKnownAction(string? action) =>
        action is not null && KnownActions.Contains(action);

    private static readonly HashSet<string> KnownActions = typeof(AuditAction)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Returns whether the key name looks like a secret and must never be stored.</summary>
    public static bool IsForbiddenKey(string key) => ForbiddenKey().IsMatch(key);

    /// <summary>Formats whitelisted details as "key=value; key=value" without control characters, at most 2000 chars.</summary>
    public static string Sanitize(string action, IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0 || !Whitelist.TryGetValue(action, out var allowed))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var key in allowed)
        {
            // Look the key up case-insensitively, but never trust a secret-looking name even if it were whitelisted.
            var match = details.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key is null || IsForbiddenKey(key))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append("; ");
            }

            sb.Append(key).Append('=').Append(CleanValue(match.Value));
        }

        return sb.Length <= MaxLength ? sb.ToString() : sb.ToString(0, MaxLength);
    }

    /// <summary>Replaces control characters (CR/LF/tab...) and Unicode line separators with spaces, so no fake log lines can be built.</summary>
    public static string ReplaceControlChars(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]) || chars[i] is '\u2028' or '\u2029')
            {
                chars[i] = ' ';
            }
        }

        return new string(chars);
    }

    // ';' separates pairs, so inside a value it is replaced to prevent fake extra pairs.
    private static string CleanValue(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : ReplaceControlChars(value).Replace(';', ',');

    [GeneratedRegex("passw|pwd|hash|salt|token|secret|credential|apikey|api_key|private", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenKey();
}
