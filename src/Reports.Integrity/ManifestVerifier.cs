using System.Security.Cryptography;
using System.Text;

namespace Reports.Integrity;

/// <summary>Checks a signed release manifest and the files it lists.</summary>
public sealed class ManifestVerifier
{
    private readonly ISignatureVerifier _signatureVerifier;

    /// <summary>Creates a verifier that uses the given signature verifier.</summary>
    public ManifestVerifier(ISignatureVerifier signatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        _signatureVerifier = signatureVerifier;
    }

    /// <summary>Returns true if the signature matches the manifest bytes.</summary>
    public bool VerifyManifestSignature(byte[] manifestBytes, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(manifestBytes);
        ArgumentNullException.ThrowIfNull(signature);
        return _signatureVerifier.Verify(manifestBytes, signature);
    }

    /// <summary>Recomputes the SHA-256 of every listed file and reports missing and modified ones.</summary>
    public IntegrityReport VerifyFiles(FileManifest manifest, string baseDir)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var root = Path.GetFullPath(baseDir);
        var rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var missing = new List<string>();
        var modified = new List<string>();

        foreach (var entry in manifest.Files)
        {
            var full = Path.GetFullPath(Path.Combine(root, entry.Path));
            if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new IntegrityException($"Manifest path leaves the base directory: {entry.Path}");
            }

            if (!File.Exists(full))
            {
                missing.Add(entry.Path);
                continue;
            }

            var actual = Encoding.ASCII.GetBytes(Sha256Mdc.ComputeFileHex(full));
            var expected = Encoding.ASCII.GetBytes(entry.Sha256.ToLowerInvariant());
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                modified.Add(entry.Path);
            }
        }

        return new IntegrityReport(true, missing, modified);
    }

    /// <summary>Checks the manifest signature first, then the files; the report is invalid if either fails.</summary>
    public IntegrityReport Verify(string baseDir, byte[] manifestBytes, byte[] signature)
    {
        var signatureValid = VerifyManifestSignature(manifestBytes, signature);
        var manifest = FileManifest.Parse(manifestBytes);
        var files = VerifyFiles(manifest, baseDir);
        return files with { SignatureValid = signatureValid };
    }
}
