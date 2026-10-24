using Microsoft.EntityFrameworkCore;

namespace Reports.Data;

/// <summary>
/// Фабрика контекстів для настільного застосунку: кожна операція отримує
/// власний короткоживучий контекст, тому вікна не ділять між собою
/// відстеження змін і не заважають одне одному.
/// </summary>
public sealed class SqliteContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteContextFactory(string databasePath)
    {
        DatabasePath = databasePath;

        var folder = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
    }

    public string DatabasePath { get; }

    public AppDbContext CreateDbContext() => new(_options);
}
