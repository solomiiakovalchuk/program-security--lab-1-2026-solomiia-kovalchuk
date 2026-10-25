using Google.Apis.Sheets.v4;

namespace Reports.Integration;

/// <summary>Level of access to the Google Sheet that a service is created for.</summary>
public enum GoogleAccess
{
    /// <summary>Import only: spreadsheets.readonly.</summary>
    ReadOnly,

    /// <summary>Upload: full spreadsheets scope, only when upload is enabled.</summary>
    ReadWrite
}

/// <summary>Creates Sheets services for a requested access level.</summary>
public interface IGoogleServiceFactory
{
    /// <summary>Creates a Sheets service with the minimal scope for the access level.</summary>
    Task<SheetsService> CreateServiceAsync(GoogleAccess access, CancellationToken ct = default);
}

/// <summary>Pure mapping from access level to OAuth scope and token store name.</summary>
public static class GoogleScopes
{
    /// <summary>Scopes requested for the access level, and nothing broader.</summary>
    public static string[] For(GoogleAccess access) => access switch
    {
        GoogleAccess.ReadOnly => new[] { SheetsService.Scope.SpreadsheetsReadonly },
        GoogleAccess.ReadWrite => new[] { SheetsService.Scope.Spreadsheets },
        _ => throw new ArgumentOutOfRangeException(nameof(access))
    };

    /// <summary>Folder name of the token store for the access level.</summary>
    public static string TokenFolderName(GoogleAccess access) => access switch
    {
        GoogleAccess.ReadOnly => "token-ro",
        GoogleAccess.ReadWrite => "token-rw",
        _ => throw new ArgumentOutOfRangeException(nameof(access))
    };
}
