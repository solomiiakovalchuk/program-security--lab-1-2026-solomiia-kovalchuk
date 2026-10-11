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

    /// <summary>Creates the authentication service.</summary>
    public AuthenticationService(
        IAccountRepository repository,
        IPasswordHasher hasher,
        ISessionContext sessionContext,
        Func<DateTime>? clock = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _clock = clock ?? (() => DateTime.UtcNow);
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
            return LoginOutcome.Failed;
        }

        if (account.IsBlocked || account.IsLockedAt(now))
        {
            _hasher.VerifyAgainstDummy(password);
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
            return LoginOutcome.Failed;
        }

        account.RegisterSuccessfulLogin(now);
        if (_hasher.NeedsRehash(storedHash))
        {
            account.SetPasswordHash(_hasher.HashPassword(password), account.MustChangePassword);
        }

        await _repository.UpdateAsync(account);
        _sessionContext.Current = new Session(account.Id, account.Login, account.Role);

        return account.MustChangePassword ? LoginOutcome.RequiresPasswordChange : LoginOutcome.Success;
    }

    /// <summary>Ends the current session.</summary>
    public Task LogoutAsync()
    {
        _sessionContext.Current = null;
        return Task.CompletedTask;
    }
}
