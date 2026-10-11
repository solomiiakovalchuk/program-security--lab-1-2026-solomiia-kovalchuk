namespace Reports.Core.Identity.Abstractions;

/// <summary>Holds the current session of the application.</summary>
public interface ISessionContext
{
    /// <summary>The current session, or null when nobody is logged in.</summary>
    Session? Current { get; set; }
}
