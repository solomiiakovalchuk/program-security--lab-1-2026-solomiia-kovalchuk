using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests for <see cref="BootstrapService"/>.
/// </summary>
public sealed class BootstrapServiceTests
{
    [Fact]
    public async Task Bootstrap_CreatesBuiltInAdmin_WhenRepositoryEmpty()
    {
        IAccountRepository repository = new InMemoryAccountRepository();
        var bootstrap = new BootstrapService();

        await bootstrap.EnsureBuiltInAdminAsync(repository);
        await bootstrap.EnsureBuiltInAdminAsync(repository);

        var accounts = await repository.GetAllAsync();
        var admin = Assert.Single(accounts);
        Assert.Equal("ADMIN", admin.Login);
        Assert.Equal(Role.Administrator, admin.Role);
        Assert.True(admin.IsBuiltIn);
        Assert.Null(admin.PasswordHash);
        Assert.True(admin.MustChangePassword);
    }
}
