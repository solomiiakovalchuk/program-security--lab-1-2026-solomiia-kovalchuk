using Reports.Core.Audit;
using Reports.Core.Identity.Abstractions;

namespace Reports.Audit;

/// <summary>One page of audit events with the total count of the filter.</summary>
public sealed record AuditPage(IReadOnlyList<AuditEvent> Items, int Total);

/// <summary>Administrator-only reading of the audit log (FR-31) and the chain check shown in the view (SEC-13).</summary>
public sealed class AuditQueryService
{
    private const string ViewAction = "ViewAuditLog";

    private readonly IAuditRepository _repository;
    private readonly ISessionContext _session;
    private readonly IAccessPolicy _policy;
    private readonly AuditChainVerifier _verifier;
    private readonly IAuditSink? _sink;

    /// <summary>Creates the service; <paramref name="sink"/> records denied attempts.</summary>
    public AuditQueryService(
        IAuditRepository repository,
        ISessionContext session,
        IAccessPolicy policy,
        AuditChainVerifier verifier,
        IAuditSink? sink = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _sink = sink;
    }

    /// <summary>Returns a page ordered by time descending, then id descending; throws if the role may not view the log.</summary>
    public async Task<AuditPage> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        AuditAccessGuard.Demand(_session, _policy, _sink, ViewAction);

        var items = await _repository.QueryAsync(query, ct).ConfigureAwait(false);
        var total = await _repository.CountAsync(query, ct).ConfigureAwait(false);
        return new AuditPage(items, total);
    }

    /// <summary>Checks the hash chain so the view can warn when it is broken.</summary>
    public Task<AuditChainResult> VerifyChainAsync(CancellationToken ct = default)
    {
        AuditAccessGuard.Demand(_session, _policy, _sink, ViewAction);
        return _verifier.VerifyAsync(ct);
    }
}
