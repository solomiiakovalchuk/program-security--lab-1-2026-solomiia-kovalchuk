using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity;

/// <summary>Creates the built-in ADMIN account on first start.</summary>
public sealed class BootstrapService
{
    /// <summary>Creates the built-in ADMIN account if no accounts exist.</summary>
    public async Task EnsureBuiltInAdminAsync(IAccountRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var accounts = await repository.GetAllAsync();
        if (accounts.Count == 0)
        {
            await repository.AddAsync(Account.CreateBuiltInAdmin());
        }
    }
}
