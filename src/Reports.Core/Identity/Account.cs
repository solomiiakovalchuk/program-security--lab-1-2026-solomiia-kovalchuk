using Reports.Core.Entities;
using Reports.Core.Identity.Exceptions;

namespace Reports.Core.Identity;

/// <summary>A user account; the aggregate root of the Identity context.</summary>
/// <remarks>
/// Invariants: the built-in ADMIN cannot be blocked, demoted or deleted.
/// Only the password hash is stored; the login is trimmed, normalized and unique.
/// </remarks>
public sealed class Account : Entity
{
    private const int MinLoginLength = 3;
    private const int MaxLoginLength = 64;
    private const string BuiltInAdminLogin = "ADMIN";

    private Account(
        string login,
        string loginNormalized,
        Role role,
        bool isBuiltIn,
        bool passwordRestrictionsEnabled,
        bool mustChangePassword)
    {
        Login = login;
        LoginNormalized = loginNormalized;
        Role = role;
        IsBuiltIn = isBuiltIn;
        PasswordRestrictionsEnabled = passwordRestrictionsEnabled;
        MustChangePassword = mustChangePassword;
    }

    /// <summary>Login in its original casing, trimmed.</summary>
    public string Login { get; private set; }

    /// <summary>Normalized login used as the unique key.</summary>
    public string LoginNormalized { get; private set; }

    /// <summary>Encoded password hash, or null if no password is set.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Role of the account.</summary>
    public Role Role { get; private set; }

    /// <summary>Whether an administrator has blocked the account.</summary>
    public bool IsBlocked { get; private set; }

    /// <summary>Whether the stricter password rules apply.</summary>
    public bool PasswordRestrictionsEnabled { get; private set; }

    /// <summary>Failed logins in a row since the last successful login.</summary>
    public int FailedAttempts { get; private set; }

    /// <summary>UTC time until which login is refused, or null when not locked.</summary>
    public DateTime? LockedUntilUtc { get; private set; }

    /// <summary>Whether the user must change the password before using the application.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Whether this is the built-in ADMIN account.</summary>
    public bool IsBuiltIn { get; private set; }

    /// <summary>Trims the login and converts it to upper-invariant case.</summary>
    public static string NormalizeLogin(string login)
    {
        ArgumentNullException.ThrowIfNull(login);
        return login.Trim().ToUpperInvariant();
    }

    /// <summary>Creates the built-in ADMIN account without a password.</summary>
    public static Account CreateBuiltInAdmin()
    {
        return new Account(
            login: BuiltInAdminLogin,
            loginNormalized: NormalizeLogin(BuiltInAdminLogin),
            role: Role.Administrator,
            isBuiltIn: true,
            passwordRestrictionsEnabled: true,
            mustChangePassword: true);
    }

    /// <summary>Creates a new user account without a password.</summary>
    public static Account Create(string login, Role role, bool passwordRestrictionsEnabled)
    {
        ArgumentNullException.ThrowIfNull(login);

        var trimmed = login.Trim();
        ValidateLogin(trimmed);

        return new Account(
            login: trimmed,
            loginNormalized: NormalizeLogin(trimmed),
            role: role,
            isBuiltIn: false,
            passwordRestrictionsEnabled: passwordRestrictionsEnabled,
            mustChangePassword: false);
    }

    /// <summary>Sets the password hash and the must-change flag.</summary>
    public void SetPasswordHash(string hash, bool mustChangePassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        PasswordHash = hash;
        MustChangePassword = mustChangePassword;
    }

    /// <summary>Counts a failed login and locks the account at the attempt limit.</summary>
    public void RegisterFailedAttempt(DateTime nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "The attempt limit must be at least 1.");
        }

        if (lockoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration), "The lockout duration must be positive.");
        }

        FailedAttempts++;
        if (FailedAttempts >= maxAttempts)
        {
            LockedUntilUtc = nowUtc + lockoutDuration;
        }
    }

    /// <summary>Clears the failed-attempt counter and any lock.</summary>
    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        _ = nowUtc;
        FailedAttempts = 0;
        LockedUntilUtc = null;
    }

    /// <summary>Clears the flag that forces a password change.</summary>
    public void ClearMustChangePassword()
    {
        MustChangePassword = false;
    }

    /// <summary>Returns whether the account is locked at the given time.</summary>
    public bool IsLockedAt(DateTime nowUtc)
    {
        return LockedUntilUtc is { } until && until > nowUtc;
    }

    /// <summary>Blocks the account from logging in.</summary>
    public void Block()
    {
        EnsureNotBuiltIn("blocked");
        IsBlocked = true;
    }

    /// <summary>Unblocks the account and clears the failed attempts and any lock.</summary>
    public void Unblock()
    {
        EnsureNotBuiltIn("unblocked");
        IsBlocked = false;
        FailedAttempts = 0;
        LockedUntilUtc = null;
    }

    /// <summary>Changes the role of the account.</summary>
    public void ChangeRole(Role newRole)
    {
        if (!Enum.IsDefined(newRole))
        {
            throw new ArgumentOutOfRangeException(nameof(newRole), "The role is not defined.");
        }

        EnsureNotBuiltIn("demoted or promoted");
        Role = newRole;
    }

    /// <summary>Throws if the account may not be deleted.</summary>
    public void EnsureCanBeDeleted()
    {
        EnsureNotBuiltIn("deleted");
    }

    private static void ValidateLogin(string login)
    {
        if (login.Length < MinLoginLength || login.Length > MaxLoginLength)
        {
            throw new DomainRuleViolationException(
                $"Login must be {MinLoginLength} to {MaxLoginLength} characters long.");
        }

        foreach (var character in login)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                throw new DomainRuleViolationException(
                    "Login may contain only letters, digits, '.', '_' and '-'.");
            }
        }
    }

    private void EnsureNotBuiltIn(string action)
    {
        if (IsBuiltIn)
        {
            throw new DomainRuleViolationException($"The built-in account '{Login}' cannot be {action}.");
        }
    }
}
