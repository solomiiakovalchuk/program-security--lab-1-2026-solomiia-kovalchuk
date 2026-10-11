namespace Reports.Core.Identity.Abstractions;

/// <summary>Checks whether a role may perform an action; actions not granted are denied.</summary>
public interface IAccessPolicy
{
    /// <summary>Returns whether the role may perform the action.</summary>
    bool IsAllowed(Role role, string action);

    /// <summary>Throws if the role may not perform the action.</summary>
    void Demand(Role role, string action);
}
