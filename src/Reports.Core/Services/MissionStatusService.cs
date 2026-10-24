using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>Cancels and restores records (a wrong submission is marked, never deleted); guarded and audited.</summary>
public sealed class MissionStatusService
{
    private readonly IMissionRepository _missions;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;

    /// <summary>Creates the service over the unguarded mission repository.</summary>
    public MissionStatusService(IMissionRepository missions, AccessGuard guard, IAuditSink? audit = null)
    {
        _missions = missions;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <summary>Marks the record Cancelled; returns null when it does not exist.</summary>
    public Task<Mission?> CancelAsync(int id, CancellationToken ct = default) =>
        SetAsync(id, cancel: true, ct);

    /// <summary>Marks the record Active again; returns null when it does not exist.</summary>
    public Task<Mission?> RestoreAsync(int id, CancellationToken ct = default) =>
        SetAsync(id, cancel: false, ct);

    /// <summary>Cancels an active record or restores a cancelled one; returns null when it does not exist.</summary>
    public async Task<Mission?> ToggleAsync(int id, CancellationToken ct = default)
    {
        // The matrix needs the right action, so look at the current status first (the read is not protected data exposure).
        var mission = await _missions.GetByIdAsync(id, ct);
        if (mission is null)
        {
            return null;
        }

        return await SetAsync(id, cancel: mission.Status != RecordStatus.Cancelled, ct);
    }

    private async Task<Mission?> SetAsync(int id, bool cancel, CancellationToken ct)
    {
        _guard.Demand(cancel ? AccessActions.CancelRecord : AccessActions.RestoreRecord);
        var action = cancel ? AuditAction.MissionCancelled : AuditAction.MissionRestored;

        try
        {
            var mission = await _missions.GetByIdAsync(id, ct);
            if (mission is null)
            {
                return null;
            }

            mission.Status = cancel ? RecordStatus.Cancelled : RecordStatus.Active;
            await _missions.SaveAsync(mission, ct);
            _audit.Record(action, AuditTarget.Mission, id.ToString(), AuditOutcome.Success);
            return mission;
        }
        catch (Exception)
        {
            _audit.Record(action, AuditTarget.Mission, id.ToString(), AuditOutcome.Failure);
            throw;
        }
    }
}
