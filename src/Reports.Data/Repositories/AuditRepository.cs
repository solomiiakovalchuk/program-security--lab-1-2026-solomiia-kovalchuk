using Microsoft.EntityFrameworkCore;
using Reports.Core.Audit;

namespace Reports.Data.Repositories;

/// <summary>Append-only EF Core storage of audit events; UPDATE is also rejected by a database trigger.</summary>
public sealed class AuditRepository : IAuditRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    /// <summary>Creates the repository over a context factory.</summary>
    public AuditRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <inheritdoc/>
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await using var db = await _factory.CreateDbContextAsync(ct);
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var db = await _factory.CreateDbContextAsync(ct);
        return await Filter(db.AuditEvents.AsNoTracking(), query)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(AuditQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var db = await _factory.CreateDbContextAsync(ct);
        return await Filter(db.AuditEvents.AsNoTracking(), query).CountAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEvent>> GetOldestFirstAsync(long afterId, int take, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AuditEvents.AsNoTracking()
            .Where(e => e.Id > afterId)
            .OrderBy(e => e.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<AuditEvent?> GetLastAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AuditEvents.AsNoTracking()
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var ticks = cutoffUtc.ToUniversalTime().Ticks;

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The trigger allows deletes only below the cutoff stored in the gate table; the gate is removed again in the same transaction.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"AuditRetentionGate\" (\"CutoffUtcTicks\") VALUES ({0})", new object[] { ticks }, ct);
        var deleted = await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditEvents\" WHERE \"OccurredAtUtc\" < {0}", new object[] { ticks }, ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditRetentionGate\"", ct);

        await tx.CommitAsync(ct);
        return deleted;
    }

    private static IQueryable<AuditEvent> Filter(IQueryable<AuditEvent> source, AuditQuery q)
    {
        if (q.FromUtc is { } from) source = source.Where(e => e.OccurredAtUtc >= from);
        if (q.ToUtc is { } to) source = source.Where(e => e.OccurredAtUtc <= to);
        if (!string.IsNullOrEmpty(q.ActorLogin)) source = source.Where(e => e.ActorLogin == q.ActorLogin);
        if (!string.IsNullOrEmpty(q.ActionType)) source = source.Where(e => e.ActionType == q.ActionType);
        if (q.Outcome is { } outcome) source = source.Where(e => e.Outcome == outcome);
        return source;
    }
}
