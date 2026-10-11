using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity.Tests;

/// <summary>
/// In-memory session holder for tests.
/// </summary>
internal sealed class TestSessionContext : ISessionContext
{
    /// <inheritdoc/>
    public Session? Current { get; set; }
}

/// <summary>
/// Controllable UTC clock for lockout tests.
/// </summary>
internal sealed class TestClock
{
    /// <summary>
    /// Creates a clock that starts at the given UTC time.
    /// </summary>
    /// <param name="utcNow">The start time.</param>
    public TestClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    /// <summary>
    /// Gets or sets the current UTC time returned by <see cref="Now"/>.
    /// </summary>
    public DateTime UtcNow { get; set; }

    /// <summary>
    /// Returns the current test time.
    /// </summary>
    /// <returns>The value of <see cref="UtcNow"/>.</returns>
    public DateTime Now() => UtcNow;
}

/// <summary>
/// Helpers that create accounts for tests.
/// </summary>
internal static class TestAccounts
{
    /// <summary>
    /// Creates an account with a password and stores it in the repository.
    /// </summary>
    /// <param name="repository">Repository to store the account in.</param>
    /// <param name="hasher">Hasher used for the password.</param>
    /// <param name="login">Login of the account.</param>
    /// <param name="password">Plain password used only for hashing in the test.</param>
    /// <param name="role">Role of the account.</param>
    /// <param name="restrictions">Whether password restrictions are enabled.</param>
    /// <returns>The stored account.</returns>
    public static async Task<Account> AddWithPasswordAsync(
        IAccountRepository repository,
        IPasswordHasher hasher,
        string login,
        string password,
        Role role = Role.Operator,
        bool restrictions = false)
    {
        var account = Account.Create(login, role, restrictions);
        account.SetPasswordHash(hasher.HashPassword(password), mustChangePassword: false);
        await repository.AddAsync(account);
        return account;
    }
}
