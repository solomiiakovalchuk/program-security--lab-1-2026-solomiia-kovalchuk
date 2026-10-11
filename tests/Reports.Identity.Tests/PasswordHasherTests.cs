using System.Security.Cryptography;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests for <see cref="PasswordHasher"/>.
/// </summary>
public sealed class PasswordHasherTests
{
    [Fact]
    public void PasswordHasher_HashAndVerify_RoundTrips()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.HashPassword("Correct#Horse1");

        Assert.True(hasher.VerifyPassword("Correct#Horse1", hash));
        Assert.StartsWith("pbkdf2-sha256$600000$", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordHasher_WrongPassword_Fails()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("Correct#Horse1");

        Assert.False(hasher.VerifyPassword("Correct#Horse2", hash));
        Assert.False(hasher.VerifyPassword("garbage", "not-a-valid-hash"));
    }

    [Fact]
    public void PasswordHasher_NeedsRehash_DetectsOldIterationCount()
    {
        var hasher = new PasswordHasher();
        var salt = new byte[PasswordHasher.SaltSizeBytes];
        RandomNumberGenerator.Fill(salt);
        var oldHash = Rfc2898DeriveBytes.Pbkdf2("Correct#Horse1", salt, 100_000, HashAlgorithmName.SHA256, 32);
        var storedOldHash = $"pbkdf2-sha256$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(oldHash)}";

        Assert.True(hasher.NeedsRehash(storedOldHash));
        Assert.True(hasher.VerifyPassword("Correct#Horse1", storedOldHash));
        Assert.False(hasher.NeedsRehash(hasher.HashPassword("Correct#Horse1")));
    }
}
