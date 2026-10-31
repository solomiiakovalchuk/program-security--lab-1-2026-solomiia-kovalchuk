using System.Text.Json;

namespace Reports.Integrity;

/// <summary>List of critical files of a release with their SHA-256 values.</summary>
public sealed record FileManifest(string Product, string Version, IReadOnlyList<ManifestEntry> Files)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>Parses manifest JSON bytes.</summary>
    public static FileManifest Parse(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            var manifest = JsonSerializer.Deserialize<FileManifest>(json, Options);
            if (manifest is null || manifest.Files is null)
            {
                throw new IntegrityException("The manifest is empty or has no file list.");
            }

            return manifest;
        }
        catch (JsonException ex)
        {
            throw new IntegrityException("The manifest is not valid JSON.", ex);
        }
    }

    /// <summary>Serializes the manifest to JSON bytes with the file list sorted by path.</summary>
    public byte[] Serialize()
    {
        var sorted = Files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        return JsonSerializer.SerializeToUtf8Bytes(this with { Files = sorted }, Options);
    }
}
