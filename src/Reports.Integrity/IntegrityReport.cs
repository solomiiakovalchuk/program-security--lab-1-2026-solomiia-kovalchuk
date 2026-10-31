namespace Reports.Integrity;

/// <summary>Result of a release integrity check.</summary>
public sealed record IntegrityReport(
    bool SignatureValid,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> ModifiedFiles)
{
    /// <summary>True only if the signature is valid and no file is missing or modified.</summary>
    public bool IsValid => SignatureValid && MissingFiles.Count == 0 && ModifiedFiles.Count == 0;
}
