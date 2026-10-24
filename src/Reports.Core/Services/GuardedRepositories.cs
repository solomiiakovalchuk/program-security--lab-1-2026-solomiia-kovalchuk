using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>Reference data: reads are open, Save/Delete need EditReferenceData and are audited.</summary>
public sealed class GuardedReferenceRepository : IReferenceRepository
{
    private readonly IReferenceRepository _inner;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;

    /// <summary>Wraps the repository.</summary>
    public GuardedReferenceRepository(IReferenceRepository inner, AccessGuard guard, IAuditSink? audit = null)
    {
        _inner = inner;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<UavType>> GetUavTypesAsync(CancellationToken ct = default) => _inner.GetUavTypesAsync(ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TargetType>> GetTargetTypesAsync(CancellationToken ct = default) => _inner.GetTargetTypesAsync(ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MunitionType>> GetMunitionTypesAsync(CancellationToken ct = default) => _inner.GetMunitionTypesAsync(ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Settlement>> GetSettlementsAsync(CancellationToken ct = default) => _inner.GetSettlementsAsync(ct);

    /// <inheritdoc/>
    public async Task SaveAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem
    {
        _guard.Demand(AccessActions.EditReferenceData);
        var action = item.Id == 0 ? AuditAction.ReferenceAdded : AuditAction.ReferenceUpdated;
        await RunAsync(action, item, () => _inner.SaveAsync(item, ct));
    }

    /// <inheritdoc/>
    public async Task DeleteAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem
    {
        _guard.Demand(AccessActions.EditReferenceData);
        await RunAsync(AuditAction.ReferenceDeleted, item, () => _inner.DeleteAsync(item, ct));
    }

    private async Task RunAsync(string action, ReferenceItem item, Func<Task> run)
    {
        var details = new Dictionary<string, string> { ["kind"] = item.DirectoryTitle, ["name"] = item.ShortName };
        try
        {
            await run();
            _audit.Record(action, AuditTarget.ReferenceItem, item.Id.ToString(), AuditOutcome.Success, details);
        }
        catch (Exception)
        {
            _audit.Record(action, AuditTarget.ReferenceItem, item.Id.ToString(), AuditOutcome.Failure, details);
            throw;
        }
    }
}

/// <summary>People: reads are open, Save/Delete need EditPeople and are audited.</summary>
public sealed class GuardedPersonRepository : IPersonRepository
{
    private readonly IPersonRepository _inner;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;

    /// <summary>Wraps the repository.</summary>
    public GuardedPersonRepository(IPersonRepository inner, AccessGuard guard, IAuditSink? audit = null)
    {
        _inner = inner;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken ct = default) => _inner.GetAllAsync(ct);

    /// <inheritdoc/>
    public Task<Person?> FindByEmailAsync(string email, CancellationToken ct = default) => _inner.FindByEmailAsync(email, ct);

    /// <inheritdoc/>
    public async Task SaveAsync(Person person, CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.EditPeople);
        var action = person.Id == 0 ? AuditAction.PersonAdded : AuditAction.PersonUpdated;
        await RunAsync(action, person, () => _inner.SaveAsync(person, ct));
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Person person, CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.EditPeople);
        await RunAsync(AuditAction.PersonDeleted, person, () => _inner.DeleteAsync(person, ct));
    }

    private async Task RunAsync(string action, Person person, Func<Task> run)
    {
        var details = new Dictionary<string, string> { ["email"] = person.Email };
        try
        {
            await run();
            _audit.Record(action, AuditTarget.Person, person.Id.ToString(), AuditOutcome.Success, details);
        }
        catch (Exception)
        {
            _audit.Record(action, AuditTarget.Person, person.Id.ToString(), AuditOutcome.Failure, details);
            throw;
        }
    }
}

/// <summary>Journal: reading needs ViewJournal; adding an entry belongs to report generation and is not guarded here.</summary>
public sealed class GuardedJournalRepository : IJournalRepository
{
    private readonly IJournalRepository _inner;
    private readonly AccessGuard _guard;

    /// <summary>Wraps the repository.</summary>
    public GuardedJournalRepository(IJournalRepository inner, AccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.ViewJournal);
        return _inner.GetAllAsync(ct);
    }

    /// <inheritdoc/>
    public Task AddAsync(JournalEntry entry, CancellationToken ct = default) => _inner.AddAsync(entry, ct);
}

/// <summary>Responses: reading needs ViewResponses; writes go through SyncService and MissionStatusService, never through this wrapper.</summary>
public sealed class GuardedMissionRepository : IMissionRepository
{
    private readonly IMissionRepository _inner;
    private readonly AccessGuard _guard;

    /// <summary>Wraps the repository.</summary>
    public GuardedMissionRepository(IMissionRepository inner, AccessGuard guard)
    {
        _inner = inner;
        _guard = guard;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.ViewResponses);
        return _inner.GetAllAsync(ct);
    }

    /// <inheritdoc/>
    public Task<Mission?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.ViewResponses);
        return _inner.GetByIdAsync(id, ct);
    }

    /// <inheritdoc/>
    public Task<IReadOnlySet<string>> GetImportedKeysAsync(CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.ViewResponses);
        return _inner.GetImportedKeysAsync(ct);
    }

    /// <inheritdoc/>
    public Task AddRangeAsync(IEnumerable<Mission> missions, CancellationToken ct = default) =>
        throw new NotSupportedException("Use SyncService to add records.");

    /// <inheritdoc/>
    public Task SaveAsync(Mission mission, CancellationToken ct = default) =>
        throw new NotSupportedException("Use MissionStatusService to change a record.");
}
