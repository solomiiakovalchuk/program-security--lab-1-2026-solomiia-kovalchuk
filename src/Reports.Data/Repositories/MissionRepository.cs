using Microsoft.EntityFrameworkCore;
using Reports.Core.Abstractions;
using Reports.Core.Domain;

namespace Reports.Data.Repositories;

/// <summary>Подані епізоди разом із цілями, боєприпасами та довідниками.</summary>
public sealed class MissionRepository : IMissionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public MissionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await Query(db).AsNoTracking()
                              .OrderByDescending(m => m.Date)
                              .ToListAsync(ct);
    }

    public async Task<Mission?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await Query(db).FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlySet<string>> GetImportedKeysAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var keys = await db.Missions.AsNoTracking()
                                    .Select(m => m.SourceRowKey)
                                    .ToListAsync(ct);
        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task AddRangeAsync(IEnumerable<Mission> missions, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Missions.AddRange(missions);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(Mission mission, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Missions.Update(mission);
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<Mission> Query(AppDbContext db) =>
        db.Missions
          .Include(m => m.Settlement)
          .Include(m => m.Targets).ThenInclude(t => t.UavType)
          .Include(m => m.Targets).ThenInclude(t => t.TargetType)
          .Include(m => m.Targets).ThenInclude(t => t.Munitions).ThenInclude(u => u.MunitionType);
}

/// <summary>Журнал сформованих рапортів.</summary>
public sealed class JournalRepository : IJournalRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public JournalRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.JournalEntries.AsNoTracking()
                                      .OrderByDescending(j => j.GeneratedAt)
                                      .ToListAsync(ct);
    }

    public async Task AddAsync(JournalEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
