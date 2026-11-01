using System.Security.Cryptography;

namespace Reports.Integrity.Tests;

/// <summary>Startup check over a temp folder with an ephemeral ECDSA key (SEC-16).</summary>
public sealed class StartupIntegrityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "startup-integrity-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public StartupIntegrityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _key.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    private void Release(bool sign = true)
    {
        File.WriteAllText(Path.Combine(_dir, "app.dll"), "binary-1");
        File.WriteAllText(Path.Combine(_dir, "app.cfg"), "mode=demo");
        var entries = new[] { "app.dll", "app.cfg" }
            .Select(n => new ManifestEntry(n, Sha256Mdc.ComputeFileHex(Path.Combine(_dir, n)))).ToList();
        var bytes = new FileManifest("ReportsWpf", "1.0.0", entries).Serialize();
        File.WriteAllBytes(Path.Combine(_dir, StartupIntegrityCheck.ManifestFileName), bytes);
        if (sign)
        {
            File.WriteAllBytes(
                Path.Combine(_dir, StartupIntegrityCheck.SignatureFileName),
                _key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        }
    }

    [Fact]
    public void IntactRelease_IsVerified()
    {
        Release();
        var result = StartupIntegrityCheck.Run(_dir, PublicKey);
        Assert.Equal(StartupIntegrityOutcome.Verified, result.Outcome);
        Assert.True(result.Report.IsValid);
    }

    [Fact]
    public void TamperedFile_Fails()
    {
        Release();
        File.WriteAllText(Path.Combine(_dir, "app.dll"), "binary-2");
        var result = StartupIntegrityCheck.Run(_dir, PublicKey);
        Assert.Equal(StartupIntegrityOutcome.Failed, result.Outcome);
        Assert.Contains("app.dll", result.Report.ModifiedFiles);
        Assert.True(result.ProblemCount >= 1);
    }

    [Fact]
    public void TamperedManifest_Fails()
    {
        Release();
        var path = Path.Combine(_dir, StartupIntegrityCheck.ManifestFileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("1.0.0", "9.9.9"));
        var result = StartupIntegrityCheck.Run(_dir, PublicKey);
        Assert.Equal(StartupIntegrityOutcome.Failed, result.Outcome);
        Assert.False(result.Report.SignatureValid);
    }

    [Fact]
    public void WrongPublicKey_Fails()
    {
        Release();
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var result = StartupIntegrityCheck.Run(_dir, Convert.ToBase64String(other.ExportSubjectPublicKeyInfo()));
        Assert.Equal(StartupIntegrityOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void NoManifest_IsReportedAsNoManifest()
    {
        var result = StartupIntegrityCheck.Run(_dir, PublicKey);
        Assert.Equal(StartupIntegrityOutcome.NoManifest, result.Outcome);
    }

    [Fact]
    public void MissingFile_Fails()
    {
        Release();
        File.Delete(Path.Combine(_dir, "app.cfg"));
        var result = StartupIntegrityCheck.Run(_dir, PublicKey);
        Assert.Equal(StartupIntegrityOutcome.Failed, result.Outcome);
        Assert.Contains("app.cfg", result.Report.MissingFiles);
    }

    [Fact]
    public void MissingSignatureFile_Fails()
    {
        Release(sign: false);
        Assert.Equal(StartupIntegrityOutcome.Failed, StartupIntegrityCheck.Run(_dir, PublicKey).Outcome);
    }

    [Fact]
    public void UnreadableManifest_Fails()
    {
        Release();
        File.WriteAllText(Path.Combine(_dir, StartupIntegrityCheck.ManifestFileName), "not json");
        Assert.Equal(StartupIntegrityOutcome.Failed, StartupIntegrityCheck.Run(_dir, PublicKey).Outcome);
    }
}
