using System.Security.Cryptography;

namespace Reports.Security;

/// <summary>AES-256-GCM implementation of ISecretProtector.</summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    /// <summary>Creates a protector with a 256-bit key.</summary>
    public AesGcmSecretProtector(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"The key must be {KeySizeBytes} bytes long.", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    /// <inheritdoc/>
    public byte[] Protect(byte[] plaintext, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using (var aes = new AesGcm(_key, TagSizeBytes))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }

        var output = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        nonce.CopyTo(output, 0);
        ciphertext.CopyTo(output, NonceSizeBytes);
        tag.CopyTo(output, NonceSizeBytes + ciphertext.Length);
        return output;
    }

    /// <inheritdoc/>
    public byte[] Unprotect(byte[] protectedData, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(protectedData);

        if (protectedData.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("The protected data is too short to be valid.");
        }

        var ciphertextLength = protectedData.Length - NonceSizeBytes - TagSizeBytes;
        var nonce = protectedData.AsSpan(0, NonceSizeBytes);
        var ciphertext = protectedData.AsSpan(NonceSizeBytes, ciphertextLength);
        var tag = protectedData.AsSpan(NonceSizeBytes + ciphertextLength, TagSizeBytes);

        var plaintext = new byte[ciphertextLength];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }
}
