using System.Security.Cryptography;

namespace Reports.Security.Tests;

/// <summary>
/// Tests for <see cref="AesGcmSecretProtector"/>.
/// </summary>
public sealed class AesGcmSecretProtectorTests
{
    private static byte[] NewKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    [Fact]
    public void AesGcm_Protect_Unprotect_RoundTrips()
    {
        var protector = new AesGcmSecretProtector(NewKey());
        var plaintext = "ya29.fake-token-for-tests"u8.ToArray();
        var associatedData = "google-oauth-token"u8.ToArray();

        var protectedData = protector.Protect(plaintext, associatedData);
        var restored = protector.Unprotect(protectedData, associatedData);

        Assert.Equal(plaintext, restored);
    }

    [Fact]
    public void AesGcm_TamperedCiphertext_ThrowsCryptographicException()
    {
        var protector = new AesGcmSecretProtector(NewKey());
        var protectedData = protector.Protect("secret value"u8.ToArray());

        // Byte 12 is the first ciphertext byte (after the 12-byte nonce).
        protectedData[12] ^= 0x01;

        // .NET throws AuthenticationTagMismatchException, a subclass of CryptographicException.
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(protectedData));
    }

    [Fact]
    public void AesGcm_NoncesAreUnique_AcrossManyCalls()
    {
        var protector = new AesGcmSecretProtector(NewKey());
        var plaintext = "same plaintext"u8.ToArray();
        var nonces = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 500; i++)
        {
            var output = protector.Protect(plaintext);
            nonces.Add(Convert.ToBase64String(output.AsSpan(0, 12)));
        }

        Assert.Equal(500, nonces.Count);
    }
}
