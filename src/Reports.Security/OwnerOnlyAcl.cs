using System.Security.AccessControl;
using System.Security.Principal;

namespace Reports.Security;

/// <summary>Restricts a file or directory to the current Windows user.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class OwnerOnlyAcl
{
    /// <summary>Disables inheritance, removes other principals and grants the current user full control.</summary>
    public static void Apply(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows user has no SID.");

        if (Directory.Exists(path))
        {
            var info = new DirectoryInfo(path);
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                user,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            info.SetAccessControl(security);
        }
        else if (File.Exists(path))
        {
            var info = new FileInfo(path);
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
            info.SetAccessControl(security);
        }
        else
        {
            throw new FileNotFoundException("The path does not exist.", path);
        }
    }
}
