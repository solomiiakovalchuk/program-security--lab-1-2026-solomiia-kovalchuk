using System.Security.Cryptography;

namespace Reports.Integrity;

/// <summary>ECDSA P-256 / SHA-256 verifier that holds only the public key.</summary>
public sealed class EcdsaSignatureVerifier : ISignatureVerifier
{
    private readonly byte[] _spki;

    /// <summary>Creates a verifier from a DER SubjectPublicKeyInfo public key.</summary>
    public EcdsaSignatureVerifier(byte[] spki)
    {
        ArgumentNullException.ThrowIfNull(spki);
        _spki = (byte[])spki.Clone();
    }

    /// <summary>Creates a verifier from a base64 SubjectPublicKeyInfo string.</summary>
    public static EcdsaSignatureVerifier FromSpkiBase64(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);

        try
        {
            return new EcdsaSignatureVerifier(Convert.FromBase64String(base64.Trim()));
        }
        catch (FormatException ex)
        {
            throw new IntegrityException("The public key is not valid base64.", ex);
        }
    }

    /// <summary>Creates a verifier from a file with a base64 SubjectPublicKeyInfo string.</summary>
    public static EcdsaSignatureVerifier FromSpkiFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromSpkiBase64(File.ReadAllText(path));
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(_spki, out _);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
