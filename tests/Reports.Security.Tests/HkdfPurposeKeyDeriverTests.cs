using System.Security.Cryptography;

namespace Reports.Security.Tests;

/// <summary>
/// Tests for <see cref="HkdfPurposeKeyDeriver"/>.
/// </summary>
public sealed class HkdfPurposeKeyDeriverTests
{
    [Fact]
    public void Hkdf_DerivesDifferentKeys_ForDifferentPurposes()
    {
        var masterKey = new byte[32];
        RandomNumberGenerator.Fill(masterKey);

        var tokenKey = HkdfPurposeKeyDeriver.DeriveKey(masterKey, "google-oauth-token");
        var secretKey = HkdfPurposeKeyDeriver.DeriveKey(masterKey, "google-client-secret");

        Assert.Equal(32, tokenKey.Length);
        Assert.NotEqual(tokenKey, secretKey);
    }

    [Fact]
    public void Hkdf_IsDeterministic_ForSameMasterKeyAndPurpose()
    {
        var masterKey = new byte[32];
        RandomNumberGenerator.Fill(masterKey);

        var first = HkdfPurposeKeyDeriver.DeriveKey(masterKey, "google-oauth-token");
        var second = HkdfPurposeKeyDeriver.DeriveKey(masterKey, "google-oauth-token");

        Assert.Equal(first, second);
    }
}
