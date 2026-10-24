using Reports.Core.Audit;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;

namespace Reports.Core.Identity;

/// <summary>Application-layer check of the access matrix for the current session (SEC-04); a denial is audited.</summary>
public sealed class AccessGuard
{
    private readonly ISessionContext _session;
    private readonly IAccessPolicy _policy;
    private readonly IAuditSink _audit;

    /// <summary>Creates the guard.</summary>
    public AccessGuard(ISessionContext session, IAccessPolicy policy, IAuditSink? audit = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <summary>Returns whether the current session may perform the action; records nothing (for UI state).</summary>
    public bool IsAllowed(string action)
    {
        var session = _session.Current;
        return session is not null && _policy.IsAllowed(session.Role, action);
    }

    /// <summary>Returns the current session or throws <see cref="AccessDeniedException"/> and records Access.Denied.</summary>
    public Session Demand(string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var session = _session.Current;
        if (session is not null && _policy.IsAllowed(session.Role, action))
        {
            return session;
        }

        var details = new Dictionary<string, string> { ["action"] = action };
        if (session is not null)
        {
            details["role"] = session.Role.ToString();
        }

        _audit.Record(AuditAction.AccessDenied, AuditTarget.Application, action, AuditOutcome.Denied, details);
        throw new AccessDeniedException(session is null
            ? $"Nobody is logged in; '{action}' is not allowed."
            : $"Role '{session.Role}' is not allowed to perform '{action}'.");
    }
}
