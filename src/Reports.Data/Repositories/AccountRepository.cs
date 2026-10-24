using Microsoft.EntityFrameworkCore;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Data.Repositories;

/// <summary>EF Core storage of accounts in the local SQLite database.</summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    /// <summary>Creates the repository over a context factory.</summary>
    public AccountRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <inheritdoc/>
    public async Task<Account?> FindByLoginAsync(string loginNormalized)
    {
        ArgumentNullException.ThrowIfNull(loginNormalized);

        await using var db = await _factory.CreateDbContextAsync();
        return await db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.LoginNormalized == loginNormalized);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var db = await _factory.CreateDbContextAsync();
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var db = await _factory.CreateDbContextAsync();
        db.Accounts.Update(account);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var db = await _factory.CreateDbContextAsync();
        db.Accounts.Remove(account);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Account>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Accounts.AsNoTracking().OrderBy(a => a.LoginNormalized).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<int> CountActiveAdministratorsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Accounts.CountAsync(a => a.Role == Role.Administrator && !a.IsBlocked);
    }
}
