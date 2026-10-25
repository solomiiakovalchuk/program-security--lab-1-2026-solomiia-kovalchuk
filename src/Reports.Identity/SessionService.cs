using Reports.Core.Audit;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity;

/// <summary>Ends the current session and records the logout.</summary>
public sealed class SessionService
{
    private readonly ISessionContext _sessionContext;
    private readonly IAuditSink _audit;

    /// <summary>Creates the session service.</summary>
    public SessionService(ISessionContext sessionContext, IAuditSink? auditSink = null)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _audit = auditSink ?? NullAuditSink.Instance;
    }

    /// <summary>Records Auth.Logout for the current session and clears it; does nothing when nobody is logged in.</summary>
    public void Logout()
    {
        var session = _sessionContext.Current;
        if (session is null)
        {
            return;
        }

        try
        {
            // Recorded while the session is still set, so the audit actor is the user who logs out.
            _audit.Record(
                AuditAction.Logout,
                AuditTarget.Account,
                session.AccountId.ToString(),
                AuditOutcome.Success,
                new Dictionary<string, string> { ["role"] = session.Role.ToString() });
        }
        finally
        {
            _sessionContext.Current = null;
        }
    }
}
