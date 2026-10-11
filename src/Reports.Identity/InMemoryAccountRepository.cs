using System.Collections.Concurrent;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;

namespace Reports.Identity;

/// <summary>In-memory account store; data is lost when the process exits.</summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, Account> _accounts = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<Account?> FindByLoginAsync(string loginNormalized)
    {
        ArgumentNullException.ThrowIfNull(loginNormalized);

        _accounts.TryGetValue(Account.NormalizeLogin(loginNormalized), out var account);
        return Task.FromResult(account);
    }

    /// <inheritdoc/>
    public Task AddAsync(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_accounts.TryAdd(account.LoginNormalized, account))
        {
            throw new DomainRuleViolationException($"An account with login '{account.Login}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_accounts.ContainsKey(account.LoginNormalized))
        {
            throw new InvalidOperationException($"Account '{account.Login}' does not exist and cannot be updated.");
        }

        _accounts[account.LoginNormalized] = account;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Account>> GetAllAsync()
    {
        IReadOnlyList<Account> snapshot = _accounts.Values.ToList();
        return Task.FromResult(snapshot);
    }

    /// <inheritdoc/>
    public Task<int> CountActiveAdministratorsAsync()
    {
        var count = _accounts.Values.Count(account => account.Role == Role.Administrator && !account.IsBlocked);
        return Task.FromResult(count);
    }
}
