using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;
using Reports.Core.Identity.Policies;

namespace Reports.Identity;

/// <summary>Administrator operations on accounts; every call demands the ManageAccounts action.</summary>
public sealed class AccountManagementService
{
    /// <summary>Access action required by every method.</summary>
    public const string ManageAccountsAction = "ManageAccounts";

    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionContext _sessionContext;
    private readonly IAccessPolicy _policy;
    private readonly IAuditSink _audit;

    /// <summary>Creates the service.</summary>
    public AccountManagementService(
        IAccountRepository repository,
        IPasswordHasher hasher,
        ISessionContext sessionContext,
        IAccessPolicy policy,
        IAuditSink? auditSink = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _audit = auditSink ?? NullAuditSink.Instance;
    }

    /// <summary>Creates an account with an initial password that must be changed at the first login.</summary>
    public async Task<AccountInfo> CreateAccountAsync(
        string login, Role role, string initialPassword, bool passwordRestrictionsEnabled = true)
    {
        var actor = Demand();
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(initialPassword);

        var account = Account.Create(login, role, passwordRestrictionsEnabled);
        EnsurePolicy(initialPassword, account);
        if (await _repository.FindByLoginAsync(account.LoginNormalized) is not null)
        {
            throw new DomainRuleViolationException($"An account with login '{account.Login}' already exists.");
        }

        account.SetPasswordHash(_hasher.HashPassword(initialPassword), mustChangePassword: true);
        await _repository.AddAsync(account);
        Record(AuditAction.AccountCreated, account, actor, ("role", role.ToString()));
        return AccountInfo.From(account);
    }

    /// <summary>Blocks an account.</summary>
    public async Task BlockAccountAsync(Guid accountId)
    {
        var actor = Demand();
        var account = await FindAsync(accountId);
        EnsureNotSelf(account, actor, "blocked");
        await EnsureNotLastActiveAdminAsync(account, "blocked");
        account.Block();
        await _repository.UpdateAsync(account);
        Record(AuditAction.AccountBlocked, account, actor);
    }

    /// <summary>Unblocks an account and clears its lockout.</summary>
    public async Task UnblockAccountAsync(Guid accountId)
    {
        var actor = Demand();
        var account = await FindAsync(accountId);
        account.Unblock();
        await _repository.UpdateAsync(account);
        Record(AuditAction.AccountUnblocked, account, actor);
    }

    /// <summary>Deletes an account.</summary>
    public async Task DeleteAccountAsync(Guid accountId)
    {
        var actor = Demand();
        var account = await FindAsync(accountId);
        EnsureNotSelf(account, actor, "deleted");
        account.EnsureCanBeDeleted();
        await EnsureNotLastActiveAdminAsync(account, "deleted");
        await _repository.DeleteAsync(account);
        Record(AuditAction.AccountDeleted, account, actor);
    }

    /// <summary>Changes the role of an account.</summary>
    public async Task ChangeRoleAsync(Guid accountId, Role newRole)
    {
        var actor = Demand();
        var account = await FindAsync(accountId);
        EnsureNotSelf(account, actor, "given a different role");
        var oldRole = account.Role;
        if (newRole != Role.Administrator)
        {
            await EnsureNotLastActiveAdminAsync(account, "demoted");
        }

        account.ChangeRole(newRole);
        await _repository.UpdateAsync(account);
        Record(AuditAction.AccountRoleChanged, account, actor, ("oldRole", oldRole.ToString()), ("newRole", newRole.ToString()));
    }

    /// <summary>Sets a temporary password, forces a change at the next login and clears the lockout.</summary>
    public async Task ResetPasswordAsync(Guid accountId, string temporaryPassword)
    {
        var actor = Demand();
        ArgumentNullException.ThrowIfNull(temporaryPassword);
        var account = await FindAsync(accountId);
        EnsureNotSelf(account, actor, "reset by themselves");
        EnsurePolicy(temporaryPassword, account);
        account.SetPasswordHash(_hasher.HashPassword(temporaryPassword), mustChangePassword: true);
        account.ClearLockout();
        await _repository.UpdateAsync(account);
        Record(AuditAction.PasswordResetByAdmin, account, actor);
    }

    /// <summary>Lists all accounts without hashes or salts.</summary>
    public async Task<IReadOnlyList<AccountInfo>> ListAccountsAsync()
    {
        Demand();
        var accounts = await _repository.GetAllAsync();
        return accounts.OrderBy(a => a.LoginNormalized, StringComparer.Ordinal).Select(AccountInfo.From).ToList();
    }

    private Session Demand()
    {
        var session = _sessionContext.Current;
        if (session is null || !_policy.IsAllowed(session.Role, ManageAccountsAction))
        {
            var details = new Dictionary<string, string> { ["action"] = ManageAccountsAction };
            if (session is not null)
            {
                details["role"] = session.Role.ToString();
            }

            _audit.Record(AuditAction.AccessDenied, AuditTarget.Account, session?.AccountId.ToString(), AuditOutcome.Denied, details);
            throw new AccessDeniedException($"The action '{ManageAccountsAction}' is not allowed.");
        }

        return session;
    }

    private async Task<Account> FindAsync(Guid id)
    {
        var all = await _repository.GetAllAsync();
        return all.FirstOrDefault(a => a.Id == id) ?? throw new DomainRuleViolationException("Account not found.");
    }

    private static void EnsureNotSelf(Account account, Session actor, string what)
    {
        if (account.Id == actor.AccountId)
        {
            throw new DomainRuleViolationException($"You cannot have your own account {what}.");
        }
    }

    private async Task EnsureNotLastActiveAdminAsync(Account account, string what)
    {
        if (account.Role == Role.Administrator && !account.IsBlocked
            && await _repository.CountActiveAdministratorsAsync() <= 1)
        {
            throw new DomainRuleViolationException($"The last active administrator cannot be {what}.");
        }
    }

    private static void EnsurePolicy(string password, Account account)
    {
        var violations = PasswordPolicy.Validate(password, account.Login, account.PasswordRestrictionsEnabled);
        if (violations.Count > 0)
        {
            throw new DomainRuleViolationException(string.Join(" ", violations));
        }
    }

    private void Record(string action, Account account, Session actor, params (string Key, string Value)[] extra)
    {
        var details = new Dictionary<string, string> { ["performerId"] = actor.AccountId.ToString() };
        foreach (var (key, value) in extra)
        {
            details[key] = value;
        }

        _audit.Record(action, AuditTarget.Account, account.Id.ToString(), AuditOutcome.Success, details);
    }
}
