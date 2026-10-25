using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity;

/// <summary>Logs the user in and out, with the failed-attempt counter and lockout.</summary>
public sealed class AuthenticationService
{
    /// <summary>Failed logins in a row that lock the account.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>How long the account stays locked after too many failed logins.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionContext _sessionContext;
    private readonly Func<DateTime> _clock;
    private readonly IAuditSink _audit;

    /// <summary>Creates the authentication service.</summary>
    public AuthenticationService(
        IAccountRepository repository,
        IPasswordHasher hasher,
        ISessionContext sessionContext,
        Func<DateTime>? clock = null,
        IAuditSink? auditSink = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _clock = clock ?? (() => DateTime.UtcNow);
        _audit = auditSink ?? NullAuditSink.Instance;
    }

    /// <summary>Attempts to log in; refused logins are reported uniformly as Failed.</summary>
    public async Task<LoginOutcome> LoginAsync(string login, string password)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(password);

        var now = _clock();
        var account = await _repository.FindByLoginAsync(Account.NormalizeLogin(login));

        if (account is null)
        {
            _hasher.VerifyAgainstDummy(password);
            RecordFailure(null, loginKnown: false, reason: null);
            return LoginOutcome.Failed;
        }

        if (account.IsBlocked || account.IsLockedAt(now))
        {
            _hasher.VerifyAgainstDummy(password);
            RecordFailure(account, loginKnown: true, account.IsBlocked ? "Blocked" : "LockedOut");
            return LoginOutcome.Failed;
        }

        // Bootstrap state: no password has been set yet. Nothing to verify; the caller asks for the initial password.
        var storedHash = account.PasswordHash;
        if (storedHash is null)
        {
            return LoginOutcome.RequiresInitialPassword;
        }

        if (!_hasher.VerifyPassword(password, storedHash))
        {
            account.RegisterFailedAttempt(now, MaxFailedAttempts, LockoutDuration);
            await _repository.UpdateAsync(account);
            RecordFailure(account, loginKnown: true, "BadPassword");
            if (account.IsLockedAt(now))
            {
                _audit.Record(
                    AuditAction.AccountLockedOut,
                    AuditTarget.Account,
                    account.Id.ToString(),
                    AuditOutcome.Failure,
                    new Dictionary<string, string> { ["lockedUntilUtc"] = account.LockedUntilUtc!.Value.ToString("O") });
            }

            return LoginOutcome.Failed;
        }

        account.RegisterSuccessfulLogin(now);
        if (_hasher.NeedsRehash(storedHash))
        {
            account.SetPasswordHash(_hasher.HashPassword(password), account.MustChangePassword);
        }

        await _repository.UpdateAsync(account);
        _sessionContext.Current = new Session(account.Id, account.Login, account.Role);
        _audit.Record(
            AuditAction.LoginSucceeded,
            AuditTarget.Account,
            account.Id.ToString(),
            AuditOutcome.Success,
            new Dictionary<string, string> { ["role"] = account.Role.ToString() });

        return account.MustChangePassword ? LoginOutcome.RequiresPasswordChange : LoginOutcome.Success;
    }

    /// <summary>Ends the current session.</summary>
    public Task LogoutAsync()
    {
        new SessionService(_sessionContext, _audit).Logout();
        return Task.CompletedTask;
    }

    // The login text is deliberately not recorded: for an unknown login it may be a mistyped password.
    private void RecordFailure(Account? account, bool loginKnown, string? reason)
    {
        var details = new Dictionary<string, string> { ["loginKnown"] = loginKnown ? "true" : "false" };
        if (reason is not null)
        {
            details["reason"] = reason;
        }

        _audit.Record(AuditAction.LoginFailed, AuditTarget.Account, account?.Id.ToString(), AuditOutcome.Failure, details);
    }
}
