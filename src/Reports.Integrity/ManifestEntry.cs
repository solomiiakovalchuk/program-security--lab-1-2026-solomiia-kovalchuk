namespace Reports.Integrity;

/// <summary>One file in the release manifest: relative path and SHA-256 hex.</summary>
public sealed record ManifestEntry(string Path, string Sha256);
