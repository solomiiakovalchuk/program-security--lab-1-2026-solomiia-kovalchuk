using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Reports.Core.Audit;

namespace Reports.Audit;

/// <summary>Computes the SHA-256 hash chain value of an audit event (SEC-13).</summary>
public static class AuditHasher
{
    /// <summary>Returns lowercase hex of SHA-256(PrevHash || canonical content) for the event.</summary>
    public static string Compute(AuditEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var sb = new StringBuilder();
        Append(sb, e.PrevHash);
        Append(sb, e.EventUuid.ToString("D"));
        Append(sb, e.OccurredAtUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        Append(sb, e.ActorAccountId?.ToString("D"));
        Append(sb, e.ActorLogin);
        Append(sb, e.ActionType);
        Append(sb, e.TargetType.ToString());
        Append(sb, e.TargetId);
        Append(sb, e.Outcome.ToString());
        Append(sb, e.Details);
        Append(sb, e.OsUser);
        Append(sb, e.MachineName);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    // Length-prefixed fields: no concatenation of two fields can be confused with another split.
    private static void Append(StringBuilder sb, string? value)
    {
        if (value is null)
        {
            sb.Append("-1:");
            return;
        }

        sb.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
    }
}
