namespace Reports.Audit;

/// <summary>Source of the current UTC time.</summary>
public interface IClock
{
    /// <summary>Current time in UTC.</summary>
    DateTime UtcNow { get; }
}

/// <summary>Clock backed by the system time.</summary>
public sealed class SystemClock : IClock
{
    /// <summary>Shared instance.</summary>
    public static readonly SystemClock Instance = new();

    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;
}
