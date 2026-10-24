namespace Reports.Core.Audit;

/// <summary>Immutable filter and page of an audit log query.</summary>
public sealed class AuditQuery
{
    /// <summary>Largest allowed page size.</summary>
    public const int MaxTake = 1000;

    /// <summary>Creates a validated query.</summary>
    public AuditQuery(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? actorLogin = null,
        string? actionType = null,
        AuditOutcome? outcome = null,
        int skip = 0,
        int take = 100)
    {
        if (fromUtc is { } from && toUtc is { } to && from > to)
        {
            throw new ArgumentException("FromUtc must not be later than ToUtc.", nameof(fromUtc));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (take < 1 || take > MaxTake)
        {
            throw new ArgumentOutOfRangeException(nameof(take), $"Take must be between 1 and {MaxTake}.");
        }

        FromUtc = fromUtc;
        ToUtc = toUtc;
        ActorLogin = actorLogin;
        ActionType = actionType;
        Outcome = outcome;
        Skip = skip;
        Take = take;
    }

    /// <summary>Inclusive lower bound of the event time.</summary>
    public DateTime? FromUtc { get; }

    /// <summary>Inclusive upper bound of the event time.</summary>
    public DateTime? ToUtc { get; }

    /// <summary>Exact actor login filter.</summary>
    public string? ActorLogin { get; }

    /// <summary>Exact action type filter.</summary>
    public string? ActionType { get; }

    /// <summary>Outcome filter.</summary>
    public AuditOutcome? Outcome { get; }

    /// <summary>Number of records to skip.</summary>
    public int Skip { get; }

    /// <summary>Page size, 1 to <see cref="MaxTake"/>.</summary>
    public int Take { get; }

    /// <summary>Returns a copy with another page.</summary>
    public AuditQuery WithPage(int skip, int take) =>
        new(FromUtc, ToUtc, ActorLogin, ActionType, Outcome, skip, take);
}
