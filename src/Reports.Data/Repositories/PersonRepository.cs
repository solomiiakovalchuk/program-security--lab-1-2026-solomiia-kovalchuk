using Microsoft.EntityFrameworkCore;
using Reports.Core.Abstractions;
using Reports.Core.Domain;

namespace Reports.Data.Repositories;

/// <summary>Довідник «Люди»: підписанти рапортів і оператори.</summary>
public sealed class PersonRepository : IPersonRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PersonRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.People.AsNoTracking()
                              .OrderBy(p => p.FullName)
                              .ToListAsync(ct);
    }

    public async Task<Person?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.People.AsNoTracking()
                              .FirstOrDefaultAsync(p => p.Email == email, ct);
    }

    public async Task SaveAsync(Person person, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        if (person.Id == 0) db.People.Add(person);
        else db.People.Update(person);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Person person, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.People.Remove(person);
        await db.SaveChangesAsync(ct);
    }
}
