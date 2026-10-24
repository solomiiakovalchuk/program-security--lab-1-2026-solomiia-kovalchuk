using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Reports.Data;
using Reports.Data.Migrations;

namespace Reports.Data.Tests;

/// <summary>A database created by the old app (EnsureCreated, no migration history) must be upgraded, not rejected.</summary>
public sealed class LegacyDatabaseTests : IDisposable
{
    private const string LegacyRow = "LEGACY-UAV";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "legacy-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, true); } catch (IOException) { }
    }

    private static string InitialMigrationId =>
        ((MigrationAttribute)Attribute.GetCustomAttribute(typeof(InitialCreate), typeof(MigrationAttribute))!).Id;

    /// <summary>Builds the schema the old app had (only InitialCreate) with one user row and no migration history.</summary>
    private async Task<SqliteContextFactory> CreateLegacyDatabaseAsync(bool keepEmptyHistoryTable)
    {
        var factory = new SqliteContextFactory(Path.Combine(_folder, "reports.db"));
        await using (var db = factory.CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(InitialMigrationId);
            await db.Database.ExecuteSqlRawAsync($"INSERT INTO UavTypes (ShortName, FullName) VALUES ('{LegacyRow}', 'legacy full name')");
            await db.Database.ExecuteSqlRawAsync(keepEmptyHistoryTable
                ? "DELETE FROM \"__EFMigrationsHistory\""
                : "DROP TABLE \"__EFMigrationsHistory\"");
        }

        SqliteConnection.ClearAllPools();
        return factory;
    }

    private static async Task<List<string>> TablesAsync(SqliteContextFactory factory)
    {
        await using var db = factory.CreateDbContext();
        return await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldDatabase_IsAdopted_KeepsDataAndGetsNewTables(bool historyTableExistsButEmpty)
    {
        var factory = await CreateLegacyDatabaseAsync(historyTableExistsButEmpty);
        Assert.DoesNotContain("Accounts", await TablesAsync(factory));

        await DatabaseInitializer.InitializeAsync(factory);

        var tables = await TablesAsync(factory);
        Assert.Contains("Accounts", tables);
        Assert.Contains("AuditEvents", tables);

        await using var db = factory.CreateDbContext();
        Assert.True(await db.UavTypes.AnyAsync(u => u.ShortName == LegacyRow), "the user's data must survive the upgrade");

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(InitialMigrationId, applied);
        Assert.Equal(2, applied.Count());
    }

    [Fact]
    public async Task OldDatabase_GetsABackupFirst_ThatHasNoNewTables()
    {
        var factory = await CreateLegacyDatabaseAsync(keepEmptyHistoryTable: false);

        await DatabaseInitializer.InitializeAsync(factory);

        var backup = factory.DatabasePath + DatabaseInitializer.LegacyBackupSuffix;
        Assert.True(File.Exists(backup));

        await using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT group_concat(name) FROM sqlite_master WHERE type = 'table'";
        var names = (string)(await tables.ExecuteScalarAsync())!;
        Assert.DoesNotContain("Accounts", names);
        Assert.Contains("UavTypes", names);

        await using var rows = connection.CreateCommand();
        rows.CommandText = $"SELECT COUNT(*) FROM UavTypes WHERE ShortName = '{LegacyRow}'";
        Assert.Equal(1L, (long)(await rows.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task OldDatabase_SecondStart_IsANoOp_AndKeepsTheFirstBackup()
    {
        var factory = await CreateLegacyDatabaseAsync(keepEmptyHistoryTable: false);
        await DatabaseInitializer.InitializeAsync(factory);
        var backup = factory.DatabasePath + DatabaseInitializer.LegacyBackupSuffix;
        var firstStamp = File.GetLastWriteTimeUtc(backup);

        await DatabaseInitializer.InitializeAsync(factory);

        Assert.Equal(firstStamp, File.GetLastWriteTimeUtc(backup));
        await using var db = factory.CreateDbContext();
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task FreshDatabase_NeedsNoBackup()
    {
        var factory = new SqliteContextFactory(Path.Combine(_folder, "fresh.db"));

        await DatabaseInitializer.InitializeAsync(factory);

        Assert.False(File.Exists(factory.DatabasePath + DatabaseInitializer.LegacyBackupSuffix));
        Assert.Contains("Accounts", await TablesAsync(factory));
    }
}
