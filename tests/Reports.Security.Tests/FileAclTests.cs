using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Reports.Security;

namespace Reports.Security.Tests;

[SupportedOSPlatform("windows")]
public class FileAclTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "rpt-acl-" + Guid.NewGuid().ToString("N"));

    public FileAclTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (Exception) { /* temp cleanup is best effort */ }
    }

    private static SecurityIdentifier CurrentUser => WindowsIdentity.GetCurrent().User!;

    private static void AssertOnlyCurrentUser(AuthorizationRuleCollection rules)
    {
        Assert.NotEmpty(rules);
        foreach (FileSystemAccessRule rule in rules)
        {
            Assert.Equal(CurrentUser, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        }

        Assert.Contains(rules.Cast<FileSystemAccessRule>(),
            r => (r.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl);
    }

    [Fact]
    public void Directory_is_restricted_to_current_user_without_inheritance()
    {
        var dir = Path.Combine(_root, "data");
        Directory.CreateDirectory(dir);

        OwnerOnlyAcl.Apply(dir);

        var security = new DirectoryInfo(dir).GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        AssertOnlyCurrentUser(rules);
        Assert.All(rules.Cast<FileSystemAccessRule>(), r =>
        {
            Assert.True(r.InheritanceFlags.HasFlag(InheritanceFlags.ContainerInherit));
            Assert.True(r.InheritanceFlags.HasFlag(InheritanceFlags.ObjectInherit));
        });
    }

    [Fact]
    public void File_created_later_in_the_directory_is_covered_by_the_owner_rule()
    {
        var dir = Path.Combine(_root, "data2");
        Directory.CreateDirectory(dir);
        OwnerOnlyAcl.Apply(dir);

        var file = Path.Combine(dir, "reports.db");
        File.WriteAllText(file, "x");

        var rules = new FileInfo(file).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
        AssertOnlyCurrentUser(rules);
    }

    [Fact]
    public void File_is_restricted_to_current_user_without_inheritance()
    {
        var file = Path.Combine(_root, "reports.db");
        File.WriteAllText(file, "x");

        OwnerOnlyAcl.Apply(file);

        var security = new FileInfo(file).GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        AssertOnlyCurrentUser(security.GetAccessRules(true, true, typeof(SecurityIdentifier)));
        Assert.Equal("x", File.ReadAllText(file));
    }

    [Fact]
    public void Missing_path_is_an_error()
    {
        Assert.Throws<FileNotFoundException>(() => OwnerOnlyAcl.Apply(Path.Combine(_root, "nope")));
    }
}
