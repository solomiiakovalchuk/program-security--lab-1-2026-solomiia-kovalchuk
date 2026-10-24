using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Reports.Core.Domain;
using Reports.Data.Migrations;

namespace Reports.Data;

/// <summary>
/// Створює базу при першому запуску і наповнює довідники прикладами,
/// щоб програму можна було відкрити й одразу спробувати без Google.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>Applies EF Core migrations and seeds the reference data; kept as the entry point name used by callers.</summary>
    public static Task EnsureCreatedAsync(
        IDbContextFactory<AppDbContext> factory,
        CancellationToken ct = default) => InitializeAsync(factory, ct);

    /// <summary>Suffix of the copy made before a database created without migrations is adopted.</summary>
    public const string LegacyBackupSuffix = ".before-migrations.bak";

    /// <summary>Adopts a database made by the old app (EnsureCreated, no migration history): backs it up and marks InitialCreate as applied.</summary>
    private static async Task BaselineLegacyDatabaseAsync(AppDbContext db, CancellationToken ct)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(ct);

        try
        {
            if (!await TableExistsAsync(connection, "Missions", ct)) return;

            var initialId = typeof(InitialCreate).GetCustomAttribute<MigrationAttribute>()!.Id;
            if (await TableExistsAsync(connection, "__EFMigrationsHistory", ct)
                && await ScalarAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $id", ct, ("$id", initialId)) > 0)
            {
                return;
            }

            var backup = connection.DataSource + LegacyBackupSuffix;
            if (!File.Exists(backup))
            {
                await using var target = new SqliteConnection($"Data Source={backup}");
                await target.OpenAsync(ct);
                connection.BackupDatabase(target);
            }

            await ScalarAsync(connection,
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL)", ct);
            await ScalarAsync(connection,
                "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, '8.0.10')", ct, ("$id", initialId));
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string name, CancellationToken ct) =>
        await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $n", ct, ("$n", name)) > 0;

    private static async Task<long> ScalarAsync(
        SqliteConnection connection, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    /// <summary>Applies EF Core migrations and seeds the reference data on first run.</summary>
    public static async Task InitializeAsync(
        IDbContextFactory<AppDbContext> factory,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await BaselineLegacyDatabaseAsync(db, ct);
        await db.Database.MigrateAsync(ct);

        if (await db.UavTypes.AnyAsync(ct)) return;

        db.UavTypes.AddRange(
            new UavType { ShortName = "FPV", FullName = "FPV-дрон" },
            new UavType { ShortName = "Кажан", FullName = "дрон «Кажан»" },
            new UavType { ShortName = "Мавік", FullName = "БпЛА типу Mavic" });

        db.TargetTypes.AddRange(
            new TargetType { ShortName = "жива сила", FullName = "живої сили противника" },
            new TargetType { ShortName = "укріплення", FullName = "укріплення противника" },
            new TargetType { ShortName = "автомобіль", FullName = "автомобільної техніки противника" },
            new TargetType { ShortName = "розвід. БпЛА", FullName = "розвідувальних безпілотних літальних апаратів противника" });

        db.MunitionTypes.AddRange(
            new MunitionType { ShortName = "ТШ-200", FullName = "тротилова шашка 200 г" },
            new MunitionType { ShortName = "ВОГ-17", FullName = "ВОГ-17" },
            new MunitionType { ShortName = "ЕД-8", FullName = "електродетонатор ЕД-8" });

        db.Settlements.AddRange(
            new Settlement { ShortName = "Ліски", Region = "Харківська" },
            new Settlement { ShortName = "Вербове", Region = "Запорізька" },
            new Settlement { ShortName = "Ямпіль", Region = "Донецька" });

        db.People.AddRange(
            new Person
            {
                Email = "commander@example.com",
                FullName = "О. КОВАЛЕНКО",
                Rank = "майор",
                Position = "Командир зенітної батареї військової частини А0000"
            },
            new Person
            {
                Email = "operator1@example.com",
                FullName = "П. ШЕВЧЕНКО",
                Rank = "сержант",
                Position = "Оператор БпЛА"
            });

        await db.SaveChangesAsync(ct);
    }
}
