using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests for the <see cref="Account"/> invariants and the <see cref="AccessPolicyService"/> matrix.
/// </summary>
public sealed class AccountAndAccessTests
{
    [Fact]
    public void BuiltInAdmin_CannotBeBlocked_OrDeleted_OrDemoted()
    {
        var admin = Account.CreateBuiltInAdmin();

        Assert.Throws<DomainRuleViolationException>(() => admin.Block());
        Assert.Throws<DomainRuleViolationException>(() => admin.Unblock());
        Assert.Throws<DomainRuleViolationException>(() => admin.ChangeRole(Role.Operator));
        Assert.Throws<DomainRuleViolationException>(() => admin.EnsureCanBeDeleted());

        Assert.False(admin.IsBlocked);
        Assert.Equal(Role.Administrator, admin.Role);
    }

    [Fact]
    public void Account_Create_RejectsInvalidLogin()
    {
        Assert.Throws<DomainRuleViolationException>(() => Account.Create("ab", Role.Operator, false));
        Assert.Throws<DomainRuleViolationException>(() => Account.Create(new string('x', 65), Role.Operator, false));
        Assert.Throws<DomainRuleViolationException>(() => Account.Create("bad login!", Role.Operator, false));
    }

    [Fact]
    public void Account_Create_TrimsLoginAndNormalizesIt()
    {
        var account = Account.Create("  Operator.One  ", Role.Operator, false);

        Assert.Equal("Operator.One", account.Login);
        Assert.Equal("OPERATOR.ONE", account.LoginNormalized);
        Assert.Null(account.PasswordHash);
    }

    [Fact]
    public void AccessPolicy_DeniesByDefault_AndAllowsPerMatrix()
    {
        IAccessPolicy policy = new AccessPolicyService();

        Assert.False(policy.IsAllowed(Role.Operator, "ManageAccounts"));
        Assert.True(policy.IsAllowed(Role.Administrator, "ManageAccounts"));

        Assert.True(policy.IsAllowed(Role.Operator, "GenerateReport"));
        Assert.True(policy.IsAllowed(Role.Administrator, "GenerateReport"));

        Assert.False(policy.IsAllowed(Role.Operator, "SomeUnlistedAction"));
        Assert.False(policy.IsAllowed(Role.Administrator, "SomeUnlistedAction"));

        Assert.Throws<AccessDeniedException>(() => policy.Demand(Role.Operator, "ViewAuditLog"));
        policy.Demand(Role.Administrator, "ViewAuditLog");
    }
}
