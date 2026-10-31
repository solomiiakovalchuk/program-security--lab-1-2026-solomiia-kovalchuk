using System.Security.Cryptography;
using System.Text;

namespace Reports.Integrity.Tests;

/// <summary>
/// Tests for the signed release manifest. Each test makes its own ephemeral ECDSA P-256 key and temp folder.
/// </summary>
public sealed class IntegrityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "integrity-" + Guid.NewGuid().ToString("N"));

    public IntegrityTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static byte[] Sign(ECDsa key, byte[] data) =>
        key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    private static EcdsaSignatureVerifier VerifierFor(ECDsa key) =>
        new(key.ExportSubjectPublicKeyInfo());

    private (FileManifest Manifest, byte[] Bytes) BuildRelease()
    {
        File.WriteAllText(Path.Combine(_dir, "app.cfg"), "mode=demo");
        File.WriteAllText(Path.Combine(_dir, "readme.txt"), "hello");
        File.WriteAllBytes(Path.Combine(_dir, "data.bin"), new byte[] { 1, 2, 3, 4, 5 });

        var entries = new[] { "app.cfg", "readme.txt", "data.bin" }
            .Select(name => new ManifestEntry(name, Sha256Mdc.ComputeFileHex(Path.Combine(_dir, name))))
            .ToList();
        var manifest = new FileManifest("ReportsWpf", "0.1.0", entries);
        return (manifest, manifest.Serialize());
    }

    [Fact]
    public void Ecdsa_Verify_AcceptsValid_RejectsTamperedDataAndSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = VerifierFor(key);
        var data = Encoding.UTF8.GetBytes("release manifest");
        var signature = Sign(key, data);

        Assert.True(verifier.Verify(data, signature));

        var badData = (byte[])data.Clone();
        badData[0] ^= 0x01;
        Assert.False(verifier.Verify(badData, signature));

        var badSignature = (byte[])signature.Clone();
        badSignature[^1] ^= 0x01;
        Assert.False(verifier.Verify(data, badSignature));
    }

    [Fact]
    public void Ecdsa_Verify_RejectsWrongPublicKey()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var data = Encoding.UTF8.GetBytes("release manifest");

        Assert.False(VerifierFor(other).Verify(data, Sign(signer, data)));
    }

    [Fact]
    public void Verify_IntactRelease_IsValid()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (_, bytes) = BuildRelease();

        var report = new ManifestVerifier(VerifierFor(key)).Verify(_dir, bytes, Sign(key, bytes));

        Assert.True(report.SignatureValid);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ModifiedFiles);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void Verify_ModifiedFile_IsReported()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (_, bytes) = BuildRelease();
        var signature = Sign(key, bytes);

        var path = Path.Combine(_dir, "data.bin");
        var content = File.ReadAllBytes(path);
        content[2] ^= 0xFF;
        File.WriteAllBytes(path, content);

        var report = new ManifestVerifier(VerifierFor(key)).Verify(_dir, bytes, signature);

        Assert.True(report.SignatureValid);
        Assert.Equal(new[] { "data.bin" }, report.ModifiedFiles);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Verify_DeletedFile_IsReportedMissing()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (_, bytes) = BuildRelease();
        var signature = Sign(key, bytes);

        File.Delete(Path.Combine(_dir, "readme.txt"));

        var report = new ManifestVerifier(VerifierFor(key)).Verify(_dir, bytes, signature);

        Assert.Equal(new[] { "readme.txt" }, report.MissingFiles);
        Assert.Empty(report.ModifiedFiles);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void Verify_TamperedManifest_BreaksSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (manifest, bytes) = BuildRelease();
        var signature = Sign(key, bytes);

        // attacker swaps a hash in the manifest so a modified file would pass the file check
        var forged = manifest with { Version = "9.9.9" };

        var report = new ManifestVerifier(VerifierFor(key)).Verify(_dir, forged.Serialize(), signature);

        Assert.False(report.SignatureValid);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void VerifiesPythonProducedRelease()
    {
        var release = Path.Combine(AppContext.BaseDirectory, "release");
        var verifier = EcdsaSignatureVerifier.FromSpkiFile(Path.Combine(release, "release-public.spki"));
        var manifestBytes = File.ReadAllBytes(Path.Combine(release, "release.manifest.json"));
        var signature = File.ReadAllBytes(Path.Combine(release, "release.manifest.sig"));

        var report = new ManifestVerifier(verifier).Verify(Path.Combine(release, "sample-dist"), manifestBytes, signature);

        Assert.True(report.SignatureValid);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ModifiedFiles);
        Assert.True(report.IsValid);
    }
}
