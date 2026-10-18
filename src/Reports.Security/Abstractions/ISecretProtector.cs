namespace Reports.Security;

/// <summary>Authenticated encryption of small secrets before they are stored.</summary>
public interface ISecretProtector
{
    /// <summary>Encrypts and authenticates the plaintext.</summary>
    byte[] Protect(byte[] plaintext, byte[]? associatedData = null);

    /// <summary>Decrypts and verifies data made by Protect; throws if it was altered.</summary>
    byte[] Unprotect(byte[] protectedData, byte[]? associatedData = null);
}
