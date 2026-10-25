using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;
using Reports.Core.Identity.Policies;

namespace Reports.Identity;

/// <summary>Sets initial passwords and changes existing ones, applying the password policy.</summary>
public sealed class ChangePasswordService
{
    private readonly IAuditSink _audit;

    /// <summary>Creates the service; auditing is off unless a sink is given.</summary>
    public ChangePasswordService(IAuditSink? auditSink = null)
    {
        _audit = auditSink ?? NullAuditSink.Instance;
    }

    /// <summary>Sets the first password of an account that has none yet.</summary>
    public async Task SetInitialPasswordAsync(
        IAccountRepository repository,
        IPasswordHasher hasher,
        Account account,
        string newPassword)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(newPassword);

        if (account.PasswordHash is not null)
        {
            throw new DomainRuleViolationException("The initial password can be set only while no password is set.");
        }

        EnsurePolicy(newPassword, account);

        account.SetPasswordHash(hasher.HashPassword(newPassword), mustChangePassword: false);
        await repository.UpdateAsync(account);
        RecordChanged(account, "Initial");
    }

    /// <summary>Changes the password after verifying the current one.</summary>
    public async Task ChangePasswordAsync(
        IAccountRepository repository,
        IPasswordHasher hasher,
        Account account,
        string currentPassword,
        string newPassword)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);

        var storedHash = account.PasswordHash;
        if (storedHash is null)
        {
            throw new DomainRuleViolationException("No password is set for this account. Set the initial password first.");
        }

        if (!hasher.VerifyPassword(currentPassword, storedHash))
        {
            throw new DomainRuleViolationException("Current password is incorrect.");
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException("The new password must differ from the current password.");
        }

        EnsurePolicy(newPassword, account);

        account.SetPasswordHash(hasher.HashPassword(newPassword), mustChangePassword: false);
        await repository.UpdateAsync(account);
        RecordChanged(account, "Self");
    }

    private void RecordChanged(Account account, string initiator)
    {
        _audit.Record(
            AuditAction.PasswordChanged,
            AuditTarget.Account,
            account.Id.ToString(),
            AuditOutcome.Success,
            new Dictionary<string, string> { ["initiator"] = initiator });
    }

    private static void EnsurePolicy(string newPassword, Account account)
    {
        var violations = PasswordPolicy.Validate(newPassword, account.Login, account.PasswordRestrictionsEnabled);
        if (violations.Count > 0)
        {
            throw new DomainRuleViolationException(string.Join(" ", violations));
        }
    }
}
