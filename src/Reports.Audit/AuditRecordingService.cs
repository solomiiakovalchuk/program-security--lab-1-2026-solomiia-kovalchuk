using Reports.Core.Audit;
using Reports.Core.Identity.Abstractions;

namespace Reports.Audit;

/// <summary>Completes events with time, actor and host, sanitizes details, links the hash chain and appends them.</summary>
public sealed class AuditRecordingService : IAuditSink
{
    /// <summary>Actor login used before anybody has logged in.</summary>
    public const string AnonymousActor = "anonymous";

    /// <summary>Actor login used for actions done by the application itself.</summary>
    public const string SystemActor = "system";

    // One chain per process: all service instances over the same database share the lock.
    private static readonly object ChainLock = new();

    private static readonly HashSet<string> SystemActions = new(StringComparer.Ordinal)
    {
        AuditAction.ApplicationStarted,
        AuditAction.IntegrityCheckFailed,
        AuditAction.IntegrityUnsigned,
    };

    private readonly IAuditRepository _repository;
    private readonly ISessionContext _session;
    private readonly IClock _clock;
    private readonly Action<Exception>? _onError;
    private readonly string _osUser;
    private readonly string _machineName;

    /// <summary>Creates the service; <paramref name="onError"/> receives write failures (the caller is never interrupted).</summary>
    public AuditRecordingService(
        IAuditRepository repository,
        ISessionContext session,
        IClock clock,
        Action<Exception>? onError = null,
        string? osUser = null,
        string? machineName = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _onError = onError;
        _osUser = osUser ?? Environment.UserName;
        _machineName = machineName ?? Environment.MachineName;
    }

    /// <inheritdoc/>
    public void Record(
        string action,
        AuditTarget target,
        string? targetId,
        AuditOutcome outcome,
        IReadOnlyDictionary<string, string>? details = null)
    {
        try
        {
            if (!AuditDetailsSanitizer.IsKnownAction(action))
            {
                throw new ArgumentException($"Unknown audit action '{action}'.", nameof(action));
            }

            if (!Enum.IsDefined(target) || !Enum.IsDefined(outcome))
            {
                throw new ArgumentException("Unknown audit target or outcome.");
            }

            var session = _session.Current;
            Guid? actorId = null;
            string actorLogin;
            if (SystemActions.Contains(action))
            {
                actorLogin = SystemActor;
            }
            else if (session is null)
            {
                actorLogin = AnonymousActor;
            }
            else
            {
                actorId = session.AccountId;
                actorLogin = Limit(AuditDetailsSanitizer.ReplaceControlChars(session.Login), 64);
            }

            var draft = new AuditEvent
            {
                EventUuid = Guid.NewGuid(),
                OccurredAtUtc = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
                ActorAccountId = actorId,
                ActorLogin = actorLogin,
                ActionType = action,
                TargetType = target,
                TargetId = targetId is null ? null : Limit(AuditDetailsSanitizer.ReplaceControlChars(targetId), 200),
                Outcome = outcome,
                Details = AuditDetailsSanitizer.Sanitize(action, details),
                OsUser = Limit(AuditDetailsSanitizer.ReplaceControlChars(_osUser), 256),
                MachineName = Limit(AuditDetailsSanitizer.ReplaceControlChars(_machineName), 256),
            };

            // Task.Run keeps a UI synchronization context out of the synchronous wait.
            lock (ChainLock)
            {
                Task.Run(() => AppendLinkedAsync(draft)).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            try
            {
                _onError?.Invoke(ex);
            }
            catch
            {
                // The hook itself must not break the business operation either.
            }
        }
    }

    private async Task AppendLinkedAsync(AuditEvent draft)
    {
        var last = await _repository.GetLastAsync().ConfigureAwait(false);
        var linked = new AuditEvent
        {
            EventUuid = draft.EventUuid,
            OccurredAtUtc = draft.OccurredAtUtc,
            ActorAccountId = draft.ActorAccountId,
            ActorLogin = draft.ActorLogin,
            ActionType = draft.ActionType,
            TargetType = draft.TargetType,
            TargetId = draft.TargetId,
            Outcome = draft.Outcome,
            Details = draft.Details,
            OsUser = draft.OsUser,
            MachineName = draft.MachineName,
            PrevHash = last?.Hash ?? string.Empty,
        };

        var hashed = new AuditEvent
        {
            EventUuid = linked.EventUuid,
            OccurredAtUtc = linked.OccurredAtUtc,
            ActorAccountId = linked.ActorAccountId,
            ActorLogin = linked.ActorLogin,
            ActionType = linked.ActionType,
            TargetType = linked.TargetType,
            TargetId = linked.TargetId,
            Outcome = linked.Outcome,
            Details = linked.Details,
            OsUser = linked.OsUser,
            MachineName = linked.MachineName,
            PrevHash = linked.PrevHash,
            Hash = AuditHasher.Compute(linked),
        };

        await _repository.AppendAsync(hashed).ConfigureAwait(false);
    }

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
}
