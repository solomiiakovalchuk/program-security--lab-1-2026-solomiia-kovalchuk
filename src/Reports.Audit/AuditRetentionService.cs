using System.Globalization;
using Reports.Core.Audit;
using Reports.Core.Identity.Abstractions;

namespace Reports.Audit;

/// <summary>Administrator-only deletion of old audit records, allowed only after a full export that covers them.</summary>
public sealed class AuditRetentionService
{
    /// <summary>Default retention period in days.</summary>
    public const int DefaultDays = 365;

    /// <summary>Smallest allowed retention period in days.</summary>
    public const int MinimumDays = 90;

    /// <summary>Export scope value meaning the export had no filters from the start of the log.</summary>
    public const string ScopeAll = "all";

    private const string RetentionAction = "ExportAuditLog";

    private readonly IAuditRepository _repository;
    private readonly ISessionContext _session;
    private readonly IAccessPolicy _policy;
    private readonly IAuditSink _sink;
    private readonly IClock _clock;

    /// <summary>Creates the service; retention shorter than 90 days is rejected.</summary>
    public AuditRetentionService(
        IAuditRepository repository,
        ISessionContext session,
        IAccessPolicy policy,
        IAuditSink sink,
        IClock clock,
        int retentionDays = DefaultDays)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        if (retentionDays < MinimumDays)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), $"Retention must be at least {MinimumDays} days.");
        }

        RetentionDays = retentionDays;
    }

    /// <summary>Records older than this many days are eligible for deletion.</summary>
    public int RetentionDays { get; }

    /// <summary>Deletes records older than the retention period and returns how many were removed.</summary>
    /// <exception cref="InvalidOperationException">No successful full export covers the records to delete.</exception>
    public async Task<int> ApplyAsync(CancellationToken ct = default)
    {
        AuditAccessGuard.Demand(_session, _policy, _sink, RetentionAction);

        var now = _clock.UtcNow.ToUniversalTime();
        var cutoff = now.AddDays(-RetentionDays);

        if (!await HasCoveringExportAsync(cutoff, ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Export the audit log before applying retention.");
        }

        var deleted = await _repository.DeleteOlderThanAsync(cutoff, ct).ConfigureAwait(false);
        _sink.Record(
            AuditAction.AuditRetentionApplied,
            AuditTarget.Audit,
            null,
            AuditOutcome.Success,
            new Dictionary<string, string>
            {
                ["cutoff"] = cutoff.ToString("O", CultureInfo.InvariantCulture),
                ["deleted"] = deleted.ToString(CultureInfo.InvariantCulture),
            });
        return deleted;
    }

    private async Task<bool> HasCoveringExportAsync(DateTime cutoff, CancellationToken ct)
    {
        var query = new AuditQuery(actionType: AuditAction.AuditExported, outcome: AuditOutcome.Success, take: 100);
        var exports = await _repository.QueryAsync(query, ct).ConfigureAwait(false);

        foreach (var export in exports)
        {
            var details = ParseDetails(export.Details);
            if (details.TryGetValue("scope", out var scope) && scope == ScopeAll
                && details.TryGetValue("to", out var to)
                && DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var toUtc)
                && toUtc.ToUniversalTime() >= cutoff)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> ParseDetails(string details)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in details.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i > 0)
            {
                result[part[..i]] = part[(i + 1)..];
            }
        }

        return result;
    }
}
