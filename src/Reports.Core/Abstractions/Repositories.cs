using Reports.Core.Domain;

namespace Reports.Core.Abstractions;

/// <summary>Доступ до довідників БпЛА, цілей, боєприпасів і місць.</summary>
public interface IReferenceRepository
{
    Task<IReadOnlyList<UavType>> GetUavTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TargetType>> GetTargetTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MunitionType>> GetMunitionTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Settlement>> GetSettlementsAsync(CancellationToken ct = default);

    Task SaveAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem;
    Task DeleteAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem;
}

/// <summary>Доступ до довідника «Люди» — підписантів і операторів.</summary>
public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken ct = default);
    Task<Person?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task SaveAsync(Person person, CancellationToken ct = default);
    Task DeleteAsync(Person person, CancellationToken ct = default);
}

/// <summary>Доступ до поданих епізодів.</summary>
public interface IMissionRepository
{
    /// <summary>Усі подачі з підвантаженими цілями, боєприпасами та довідниками.</summary>
    Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken ct = default);

    Task<Mission?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Ключі рядків-джерел, які вже імпортовані (захист від дублів).</summary>
    Task<IReadOnlySet<string>> GetImportedKeysAsync(CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<Mission> missions, CancellationToken ct = default);
    Task SaveAsync(Mission mission, CancellationToken ct = default);
}

/// <summary>Журнал сформованих рапортів.</summary>
public interface IJournalRepository
{
    Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
}
