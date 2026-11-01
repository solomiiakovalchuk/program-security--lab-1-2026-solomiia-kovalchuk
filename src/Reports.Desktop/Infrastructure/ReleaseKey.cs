using System.IO;

namespace Reports.Desktop.Infrastructure;

/// <summary>The release public key (base64 SPKI) embedded from config/release-public.spki; the client holds no private key.</summary>
public static class ReleaseKey
{
    private const string ResourceName = "release-public.spki";

    /// <summary>Returns the embedded key, or an empty string when the resource is missing (the check then fails).</summary>
    public static string Load()
    {
        using var stream = typeof(ReleaseKey).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return string.Empty;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
