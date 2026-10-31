using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Wpf.Ui.Controls;

namespace Reports.Desktop.Tests;

/// <summary>An unknown icon name only fails when the view is created, so check every name in source.</summary>
public class IconNamesTests
{
    private static string DesktopSourceFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReportsWpf.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Reports.Desktop");
    }

    [Fact]
    public void EverySymbolNameUsedInViewsAndViewModels_ExistsInWpfUi()
    {
        var patterns = new[]
        {
            new Regex(@"Symbol=""([A-Za-z0-9]+)"""),
            new Regex(@"\{ui:SymbolIcon\s+([A-Za-z0-9]+)\}"),
            new Regex(@"SymbolRegular\.([A-Za-z0-9]+)"),
        };

        var found = new List<(string Name, string File)>();
        foreach (var file in Directory.EnumerateFiles(DesktopSourceFolder(), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".xaml") || f.EndsWith(".cs"))
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (var pattern in patterns)
            {
                found.AddRange(pattern.Matches(text).Select(m => (m.Groups[1].Value, Path.GetFileName(file))));
            }
        }

        Assert.True(found.Count >= 10, $"expected to find the icon names, found {found.Count}");

        var unknown = found
            .Where(f => !Enum.TryParse<SymbolRegular>(f.Name, out _))
            .Select(f => $"{f.Name} ({f.File})")
            .Distinct()
            .ToList();

        Assert.True(unknown.Count == 0, "Unknown WPF-UI icon names: " + string.Join(", ", unknown));
    }
}
