using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>
/// Збирає локальні довідники, людей і статуси записів та передає їх
/// приймачу для запису у зовнішню таблицю.
/// <para>
/// Сам сервіс не вирішує, чого в таблиці бракує: він віддає повний зріз,
/// а звіряння з наявним вмістом робить приймач — лише він бачить таблицю.
/// </para>
/// </summary>
public sealed class PublishService
{
    private readonly IReferenceRepository _references;
    private readonly IPersonRepository _people;
    private readonly IMissionRepository _missions;
    private readonly AccessGuard? _guard;
    private readonly IAuditSink _audit;

    public PublishService(
        IReferenceRepository references,
        IPersonRepository people,
        IMissionRepository missions,
        AccessGuard? guard = null,
        IAuditSink? audit = null)
    {
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
        _references = references;
        _people = people;
        _missions = missions;
    }

    public async Task<PublishPayload> CollectAsync(CancellationToken ct = default)
    {
        var uav = await _references.GetUavTypesAsync(ct);
        var targets = await _references.GetTargetTypesAsync(ct);
        var munitions = await _references.GetMunitionTypesAsync(ct);
        var settlements = await _references.GetSettlementsAsync(ct);
        var people = await _people.GetAllAsync(ct);
        var missions = await _missions.GetAllAsync(ct);

        return new PublishPayload
        {
            Uav = Pairs(uav),
            Targets = Pairs(targets),
            Munitions = Pairs(munitions),
            // У блоці місць права колонка — область, а не повна назва.
            Settlements = settlements
                .Where(s => !string.IsNullOrWhiteSpace(s.ShortName))
                .Select(s => new DirectoryPair(s.ShortName.Trim(), s.Region.Trim()))
                .ToList(),
            People = people.Where(p => !string.IsNullOrWhiteSpace(p.Email)).ToList(),
            Statuses = missions
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceRowKey))
                .Select(m => new StatusChange(m.SourceRowKey, m.Status.ToMarker()))
                .ToList()
        };
    }

    /// <summary>Uploads the local data; needs PublishToGoogle when a guard is configured, and records Google.PublishCompleted.</summary>
    public async Task<PublishResult> PublishAsync(IResponseSink sink, CancellationToken ct = default)
    {
        _guard?.Demand(AccessActions.PublishToGoogle);

        try
        {
            var payload = await CollectAsync(ct);
            var result = payload.IsEmpty ? PublishResult.Nothing : await sink.PublishAsync(payload, ct);
            _audit.Record(AuditAction.GooglePublishCompleted, AuditTarget.GoogleSync, null, AuditOutcome.Success,
                new Dictionary<string, string>
                {
                    ["rows"] = (result.DirectoriesAdded + result.PeopleAdded + result.StatusesUpdated).ToString()
                });
            return result;
        }
        catch (Exception)
        {
            _audit.Record(AuditAction.GooglePublishCompleted, AuditTarget.GoogleSync, null, AuditOutcome.Failure);
            throw;
        }
    }

    private static IReadOnlyList<DirectoryPair> Pairs<T>(IEnumerable<T> items) where T : ReferenceItem =>
        items.Where(i => !string.IsNullOrWhiteSpace(i.ShortName))
             .Select(i => new DirectoryPair(i.ShortName.Trim(), i.FullName.Trim()))
             .ToList();
}
