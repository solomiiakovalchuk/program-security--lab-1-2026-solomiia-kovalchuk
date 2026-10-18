using System.Security.Cryptography;
using System.Text;

namespace Reports.Security;

/// <summary>Derives independent keys for different purposes from one master key using HKDF-SHA256.</summary>
public static class HkdfPurposeKeyDeriver
{
    /// <summary>Derives a key for the given purpose from the master key.</summary>
    public static byte[] DeriveKey(byte[] masterKey, string purpose, int keyLengthBytes = 32)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            masterKey,
            keyLengthBytes,
            salt: null,
            info: Encoding.UTF8.GetBytes(purpose));
    }
}
