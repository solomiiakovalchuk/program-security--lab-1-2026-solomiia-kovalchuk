using System.IO;
using System.Text.Json;
using Reports.Core.Services;

namespace Reports.Desktop.Infrastructure;

/// <summary>
/// Зберігає налаштування у файлі JSON поряд із базою даних.
/// Пошкоджений або відсутній файл не має валити застосунок —
/// у такому разі беруться значення за замовчуванням.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    public string Path => _path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var folder = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
    }
}
