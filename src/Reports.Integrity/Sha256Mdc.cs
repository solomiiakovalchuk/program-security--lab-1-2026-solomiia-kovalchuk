using System.Security.Cryptography;

namespace Reports.Integrity;

/// <summary>SHA-256 modification detection code (MDC) helper.</summary>
public static class Sha256Mdc
{
    /// <summary>Returns the lowercase hex SHA-256 of the data.</summary>
    public static string ComputeHex(ReadOnlySpan<byte> data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    /// <summary>Returns the lowercase hex SHA-256 of the file content.</summary>
    public static string ComputeFileHex(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
