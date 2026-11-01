namespace Reports.Integrity;

/// <summary>Outcome of the startup integrity check.</summary>
public enum StartupIntegrityOutcome
{
    /// <summary>Signature and all file hashes are valid.</summary>
    Verified,

    /// <summary>No release.manifest.json next to the application: an unsigned development build.</summary>
    NoManifest,

    /// <summary>Bad signature, modified or missing file, or an unreadable manifest: the application must not start.</summary>
    Failed
}

/// <summary>Result of <see cref="StartupIntegrityCheck.Run"/>.</summary>
public sealed record StartupIntegrityResult(StartupIntegrityOutcome Outcome, IntegrityReport Report, string Reason)
{
    /// <summary>Number of problems found (at least 1 when Failed).</summary>
    public int ProblemCount => Outcome != StartupIntegrityOutcome.Failed
        ? 0
        : Math.Max(1, Report.MissingFiles.Count + Report.ModifiedFiles.Count + (Report.SignatureValid ? 0 : 1));
}

/// <summary>Verifies release.manifest.json and its signature against the files in the application folder (SEC-16).</summary>
public static class StartupIntegrityCheck
{
    /// <summary>File name of the signed manifest.</summary>
    public const string ManifestFileName = "release.manifest.json";

    /// <summary>File name of the manifest signature.</summary>
    public const string SignatureFileName = "release.manifest.sig";

    /// <summary>Checks the application folder with the embedded base64 SPKI public key; never throws.</summary>
    public static StartupIntegrityResult Run(string baseDirectory, string publicKeyBase64)
    {
        var empty = new IntegrityReport(false, Array.Empty<string>(), Array.Empty<string>());
        try
        {
            var manifestPath = Path.Combine(baseDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return new StartupIntegrityResult(StartupIntegrityOutcome.NoManifest, empty, "no manifest (unsigned build)");
            }

            var signaturePath = Path.Combine(baseDirectory, SignatureFileName);
            if (!File.Exists(signaturePath))
            {
                return new StartupIntegrityResult(StartupIntegrityOutcome.Failed, empty, "signature file missing");
            }

            var verifier = new ManifestVerifier(EcdsaSignatureVerifier.FromSpkiBase64(publicKeyBase64));
            var report = verifier.Verify(baseDirectory, File.ReadAllBytes(manifestPath), File.ReadAllBytes(signaturePath));
            if (report.IsValid)
            {
                return new StartupIntegrityResult(StartupIntegrityOutcome.Verified, report, "verified");
            }

            var reason = !report.SignatureValid ? "signature invalid"
                : report.ModifiedFiles.Count > 0 ? "file modified"
                : "file missing";
            return new StartupIntegrityResult(StartupIntegrityOutcome.Failed, report, reason);
        }
        catch (Exception ex) when (ex is IntegrityException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new StartupIntegrityResult(StartupIntegrityOutcome.Failed, empty, "manifest unreadable");
        }
    }
}
