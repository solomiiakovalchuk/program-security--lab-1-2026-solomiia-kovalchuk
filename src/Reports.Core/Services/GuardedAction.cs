using Reports.Core.Audit;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>Runs an administrator-only action behind the guard and audits it (template rebuild, token removal).</summary>
public sealed class GuardedAction
{
    private readonly string _accessAction;
    private readonly string _auditAction;
    private readonly AuditTarget _target;
    private readonly Action _run;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;

    /// <summary>Creates the wrapper for one action.</summary>
    public GuardedAction(
        string accessAction, string auditAction, AuditTarget target, Action run,
        AccessGuard guard, IAuditSink? audit = null)
    {
        _accessAction = accessAction;
        _auditAction = auditAction;
        _target = target;
        _run = run;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <summary>Demands the access action, runs the work and records Success or Failure.</summary>
    public void Execute()
    {
        _guard.Demand(_accessAction);
        try
        {
            _run();
            _audit.Record(_auditAction, _target, null, AuditOutcome.Success);
        }
        catch (Exception)
        {
            _audit.Record(_auditAction, _target, null, AuditOutcome.Failure);
            throw;
        }
    }

    /// <summary>Wrapper that rebuilds the report template (RebuildTemplate, Template.Rebuilt).</summary>
    public static GuardedAction RebuildTemplate(Action rebuild, AccessGuard guard, IAuditSink? audit = null) =>
        new(AccessActions.RebuildTemplate, AuditAction.TemplateRebuilt, AuditTarget.Settings, rebuild, guard, audit);

    /// <summary>Wrapper that removes the saved Google tokens (ForgetToken, Google.TokenForgotten).</summary>
    public static GuardedAction ForgetToken(Action forget, AccessGuard guard, IAuditSink? audit = null) =>
        new(AccessActions.ForgetToken, AuditAction.GoogleTokenForgotten, AuditTarget.GoogleSync, forget, guard, audit);
}
