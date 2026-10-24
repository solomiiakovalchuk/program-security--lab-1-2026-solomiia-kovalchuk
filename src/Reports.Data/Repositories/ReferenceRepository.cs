using Microsoft.EntityFrameworkCore;
using Reports.Core.Abstractions;
using Reports.Core.Domain;

namespace Reports.Data.Repositories;

/// <summary>Довідники БпЛА, цілей, боєприпасів і місць у локальній базі.</summary>
public sealed class ReferenceRepository : IReferenceRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ReferenceRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<UavType>> GetUavTypesAsync(CancellationToken ct = default) =>
        await LoadAsync<UavType>(ct);

    public async Task<IReadOnlyList<TargetType>> GetTargetTypesAsync(CancellationToken ct = default) =>
        await LoadAsync<TargetType>(ct);

    public async Task<IReadOnlyList<MunitionType>> GetMunitionTypesAsync(CancellationToken ct = default) =>
        await LoadAsync<MunitionType>(ct);

    public async Task<IReadOnlyList<Settlement>> GetSettlementsAsync(CancellationToken ct = default) =>
        await LoadAsync<Settlement>(ct);

    public async Task SaveAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        if (item.Id == 0) db.Add(item);
        else db.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<T>> LoadAsync<T>(CancellationToken ct) where T : ReferenceItem
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<T>()
                       .AsNoTracking()
                       .OrderBy(x => x.ShortName)
                       .ToListAsync(ct);
    }
}
