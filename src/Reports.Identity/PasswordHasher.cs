using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity;

/// <summary>PBKDF2-HMAC-SHA256 password hasher.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>PBKDF2 iteration count for new hashes.</summary>
    public const int Iterations = 600_000;

    /// <summary>Salt size in bytes.</summary>
    public const int SaltSizeBytes = 16;

    /// <summary>Derived key size in bytes.</summary>
    public const int HashSizeBytes = 32;

    private const string Prefix = "pbkdf2-sha256";
    private const int FieldCount = 4;
    private const int MaxAcceptedIterations = 10_000_000;
    private const string DummyPassword = "placeholder-password-for-timing-only";

    internal static readonly string DummyHash = ComputeHash(DummyPassword, new byte[SaltSizeBytes], Iterations);

    /// <inheritdoc/>
    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = new byte[SaltSizeBytes];
        RandomNumberGenerator.Fill(salt);
        return ComputeHash(password, salt, Iterations);
    }

    /// <inheritdoc/>
    public bool VerifyPassword(string password, string hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hash);

        if (!TryParse(hash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <inheritdoc/>
    public bool NeedsRehash(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return !TryParse(hash, out var iterations, out _, out _) || iterations != Iterations;
    }

    /// <inheritdoc/>
    public bool VerifyAgainstDummy(string password)
    {
        _ = VerifyPassword(password, DummyHash);
        return false;
    }

    private static string ComputeHash(string password, byte[] salt, int iterations)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return string.Join(
            "$",
            Prefix,
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool TryParse(string hash, out int iterations, out byte[] salt, out byte[] expected)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        expected = Array.Empty<byte>();

        var parts = hash.Split('$');
        if (parts.Length != FieldCount || parts[0] != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations < 1
            || iterations > MaxAcceptedIterations)
        {
            return false;
        }

        if (!TryDecodeBase64(parts[2], out salt) || salt.Length == 0)
        {
            return false;
        }

        if (!TryDecodeBase64(parts[3], out expected) || expected.Length == 0)
        {
            return false;
        }

        return true;
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[value.Length];
        if (Convert.TryFromBase64String(value, buffer, out var written))
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }
}
