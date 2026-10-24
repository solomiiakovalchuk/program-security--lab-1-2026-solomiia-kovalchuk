using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Reports.Core.Identity;
using Reports.Data;
using Reports.Data.Repositories;

namespace Reports.Data.Tests;

public sealed class AccountRepositoryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "data-tests-" + Guid.NewGuid().ToString("N"));
    private readonly SqliteContextFactory _factory;
    private readonly AccountRepository _repo;

    public AccountRepositoryTests()
    {
        _factory = new SqliteContextFactory(Path.Combine(_folder, "test.db"));
        DatabaseInitializer.InitializeAsync(_factory).GetAwaiter().GetResult();
        _repo = new AccountRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Migration_OnFreshDatabase_CreatesAllTablesAndSeeds()
    {
        await using var db = _factory.CreateDbContext();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();

        foreach (var t in new[] { "Accounts", "AuditEvents", "Missions", "People", "UavTypes", "TargetEngagements", "MunitionUses", "JournalEntries", "Settlements" })
        {
            Assert.Contains(t, tables);
        }

        Assert.True(await db.UavTypes.AnyAsync());
    }

    [Fact]
    public async Task Migration_RunTwice_IsIdempotent()
    {
        await DatabaseInitializer.InitializeAsync(_factory);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(3, await db.UavTypes.CountAsync());
    }

    [Fact]
    public async Task AddAndFind_RoundTripsAllFields()
    {
        var a = Account.Create("Alice", Role.Operator, true);
        a.SetPasswordHash("hash-value", true);
        a.RegisterFailedAttempt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1, TimeSpan.FromMinutes(5));
        await _repo.AddAsync(a);

        var found = await _repo.FindByLoginAsync(Account.NormalizeLogin("alice"));

        Assert.NotNull(found);
        Assert.Equal(a.Id, found!.Id);
        Assert.Equal("Alice", found.Login);
        Assert.Equal("ALICE", found.LoginNormalized);
        Assert.Equal("hash-value", found.PasswordHash);
        Assert.Equal(Role.Operator, found.Role);
        Assert.True(found.MustChangePassword);
        Assert.Equal(1, found.FailedAttempts);
        Assert.Equal(a.LockedUntilUtc, found.LockedUntilUtc);
        Assert.Equal(DateTimeKind.Utc, found.LockedUntilUtc!.Value.Kind);
        Assert.False(found.IsBuiltIn);
        Assert.Null(await _repo.FindByLoginAsync("NOBODY"));
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        var a = Account.Create("bobby", Role.Operator, false);
        await _repo.AddAsync(a);

        var loaded = (await _repo.FindByLoginAsync("BOBBY"))!;
        loaded.Block();
        loaded.ChangeRole(Role.Administrator);
        await _repo.UpdateAsync(loaded);

        var again = (await _repo.FindByLoginAsync("BOBBY"))!;
        Assert.True(again.IsBlocked);
        Assert.Equal(Role.Administrator, again.Role);
    }

    [Fact]
    public async Task GetAll_ReturnsAllIncludingBlocked()
    {
        var blocked = Account.Create("carol", Role.Operator, false);
        blocked.Block();
        await _repo.AddAsync(blocked);
        await _repo.AddAsync(Account.Create("dave", Role.Operator, false));

        var all = await _repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task CountActiveAdministrators_IgnoresBlockedAndOperators()
    {
        await _repo.AddAsync(Account.CreateBuiltInAdmin());
        await _repo.AddAsync(Account.Create("admin2", Role.Administrator, false));
        var admin3 = Account.Create("admin3", Role.Administrator, false);
        admin3.Block();
        await _repo.AddAsync(admin3);
        await _repo.AddAsync(Account.Create("opera", Role.Operator, false));

        Assert.Equal(2, await _repo.CountActiveAdministratorsAsync());
    }

    [Fact]
    public async Task DuplicateNormalizedLogin_IsRejectedByUniqueIndex()
    {
        await _repo.AddAsync(Account.Create("Eve", Role.Operator, false));
        await Assert.ThrowsAsync<DbUpdateException>(() => _repo.AddAsync(Account.Create("eVE", Role.Operator, false)));
    }

    [Fact]
    public async Task Delete_RemovesAccount()
    {
        await _repo.AddAsync(Account.Create("frank", Role.Operator, false));

        await _repo.DeleteAsync((await _repo.FindByLoginAsync("FRANK"))!);

        Assert.Null(await _repo.FindByLoginAsync("FRANK"));
        Assert.Empty(await _repo.GetAllAsync());
    }
}
