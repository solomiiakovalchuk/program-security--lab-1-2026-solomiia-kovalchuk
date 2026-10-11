namespace Reports.Core.Identity;

/// <summary>The authenticated user for one login, used as the actor in other modules and the audit log.</summary>
public sealed record Session(Guid AccountId, string Login, Role Role);
