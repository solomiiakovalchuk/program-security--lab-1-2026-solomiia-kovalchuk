using Reports.Core.Identity;

namespace Reports.Identity;

/// <summary>Account data for lists and screens; never carries the hash or salt.</summary>
public sealed record AccountInfo(
    Guid Id,
    string Login,
    Role Role,
    bool IsBlocked,
    bool IsBuiltIn,
    bool PasswordRestrictionsEnabled,
    bool MustChangePassword,
    bool HasPassword,
    int FailedAttempts,
    DateTime? LockedUntilUtc)
{
    internal static AccountInfo From(Account a) => new(
        a.Id, a.Login, a.Role, a.IsBlocked, a.IsBuiltIn, a.PasswordRestrictionsEnabled,
        a.MustChangePassword, a.PasswordHash is not null, a.FailedAttempts, a.LockedUntilUtc);
}
