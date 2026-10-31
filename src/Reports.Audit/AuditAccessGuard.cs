using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;

namespace Reports.Audit;

/// <summary>Demands an access-policy action for the current session and records a denial.</summary>
internal static class AuditAccessGuard
{
    internal static Session Demand(ISessionContext sessionContext, IAccessPolicy policy, IAuditSink? sink, string action)
    {
        var session = sessionContext.Current;
        try
        {
            if (session is null)
            {
                throw new AccessDeniedException("Nobody is logged in.");
            }

            policy.Demand(session.Role, action);
            return session;
        }
        catch (AccessDeniedException)
        {
            sink?.Record(
                AuditAction.AccessDenied,
                AuditTarget.Audit,
                action,
                AuditOutcome.Denied,
                new Dictionary<string, string>
                {
                    ["action"] = action,
                    ["role"] = session?.Role.ToString() ?? "none",
                });
            throw;
        }
    }
}
