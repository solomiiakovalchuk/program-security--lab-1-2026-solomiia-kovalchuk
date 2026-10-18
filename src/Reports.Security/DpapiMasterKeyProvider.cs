using System.Security.Cryptography;

namespace Reports.Security;

/// <summary>Stores the master key in a file protected by DPAPI for the current Windows user.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DpapiMasterKeyProvider : IMasterKeyProvider
{
    private const int KeySizeBytes = 32;

    private readonly string _keyFilePath;

    /// <summary>Creates a provider that stores the key at the given path.</summary>
    public DpapiMasterKeyProvider(string keyFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFilePath);
        _keyFilePath = keyFilePath;
    }

    /// <inheritdoc/>
    public byte[] GetOrCreateMasterKey()
    {
        if (File.Exists(_keyFilePath))
        {
            var protectedKey = File.ReadAllBytes(_keyFilePath);
            var key = ProtectedData.Unprotect(protectedKey, optionalEntropy: null, DataProtectionScope.CurrentUser);
            if (key.Length != KeySizeBytes)
            {
                throw new CryptographicException("The stored master key has an unexpected length.");
            }

            return key;
        }

        var newKey = new byte[KeySizeBytes];
        RandomNumberGenerator.Fill(newKey);

        var protectedNewKey = ProtectedData.Protect(newKey, optionalEntropy: null, DataProtectionScope.CurrentUser);

        var directory = Path.GetDirectoryName(Path.GetFullPath(_keyFilePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_keyFilePath, protectedNewKey);
        return newKey;
    }
}
