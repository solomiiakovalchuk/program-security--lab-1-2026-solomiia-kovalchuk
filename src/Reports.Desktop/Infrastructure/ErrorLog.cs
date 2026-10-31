using System.IO;

namespace Reports.Desktop.Infrastructure;

/// <summary>Writes diagnostics to error.log in the data folder; never throws and never gets secrets.</summary>
public static class ErrorLog
{
    /// <summary>Appends the exception text under the title; failures of the log itself are ignored.</summary>
    public static void Write(string title, Exception exception)
    {
        try
        {
            var folder = AppDataFolder.Resolve();
            Directory.CreateDirectory(folder);
            File.AppendAllText(
                Path.Combine(folder, "error.log"),
                $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {title} ==={Environment.NewLine}" +
                exception + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
            // Журнал помилок сам по собі не має валити застосунок.
        }
    }
}
