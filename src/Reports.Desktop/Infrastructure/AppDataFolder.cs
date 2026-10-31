using System.IO;

namespace Reports.Desktop.Infrastructure;

/// <summary>Resolves the application data folder; env var REPORTS_DATA_DIR overrides %AppData%\Рапорти.</summary>
public static class AppDataFolder
{
    public const string EnvVar = "REPORTS_DATA_DIR";

    /// <summary>Returns REPORTS_DATA_DIR when set and non-empty, otherwise %AppData%\Рапорти.</summary>
    public static string Resolve()
    {
        var overridden = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(overridden))
            return overridden;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Рапорти");
    }
}
