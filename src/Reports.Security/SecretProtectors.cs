namespace Reports.Security;

/// <summary>Builds per-purpose secret protectors from the master key.</summary>
public static class SecretProtectors
{
    /// <summary>Purpose of the Google OAuth token cache.</summary>
    public const string GoogleOAuthTokenPurpose = "google-oauth-token";

    /// <summary>Purpose of the encrypted Google client secret file.</summary>
    public const string GoogleClientSecretPurpose = "google-client-secret";

    /// <summary>Creates an AES-256-GCM protector whose key is derived by HKDF for the purpose.</summary>
    public static ISecretProtector ForPurpose(IMasterKeyProvider masterKeys, string purpose)
    {
        ArgumentNullException.ThrowIfNull(masterKeys);
        var key = HkdfPurposeKeyDeriver.DeriveKey(masterKeys.GetOrCreateMasterKey(), purpose);
        return new AesGcmSecretProtector(key);
    }
}
