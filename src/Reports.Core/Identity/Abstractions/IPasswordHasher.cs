namespace Reports.Core.Identity.Abstractions;

/// <summary>Hashes and verifies passwords.</summary>
public interface IPasswordHasher
{
    /// <summary>Creates a salted hash of the password.</summary>
    string HashPassword(string password);

    /// <summary>Checks a password against an encoded hash.</summary>
    bool VerifyPassword(string password, string hash);

    /// <summary>Returns whether the hash uses outdated parameters.</summary>
    bool NeedsRehash(string hash);

    /// <summary>Runs a dummy verification and always returns false.</summary>
    bool VerifyAgainstDummy(string password);
}
