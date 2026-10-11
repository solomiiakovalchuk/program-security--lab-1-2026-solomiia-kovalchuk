using Reports.Core.Identity;

namespace Reports.Core.Identity.Abstractions;

/// <summary>Storage contract for accounts.</summary>
public interface IAccountRepository
{
    /// <summary>Finds an account by its normalized login, or returns null.</summary>
    Task<Account?> FindByLoginAsync(string loginNormalized);

    /// <summary>Adds a new account.</summary>
    Task AddAsync(Account account);

    /// <summary>Saves changes to an existing account.</summary>
    Task UpdateAsync(Account account);

    /// <summary>Returns all accounts, including blocked ones.</summary>
    Task<IReadOnlyList<Account>> GetAllAsync();

    /// <summary>Counts administrators that are not blocked.</summary>
    Task<int> CountActiveAdministratorsAsync();
}
