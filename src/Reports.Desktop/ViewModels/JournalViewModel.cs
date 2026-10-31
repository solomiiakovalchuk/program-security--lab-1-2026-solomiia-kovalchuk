using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Reports.Core.Domain;
using Reports.Desktop.Infrastructure;

namespace Reports.Desktop.ViewModels;

/// <summary>Вкладка «Журнал»: аудит сформованих рапортів.</summary>
public sealed class JournalViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly Action<string> _report;
    private JournalEntry? _selected;

    public JournalViewModel(AppServices services, Action<string> report)
    {
        _services = services;
        _report = report;

        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        OpenCommand = new RelayCommand(OpenSelected,
            () => Selected is not null && File.Exists(Selected.FilePath));
    }

    public ObservableCollection<JournalEntry> Entries { get; } = new();

    public JournalEntry? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand OpenCommand { get; }

    public async Task LoadAsync()
    {
        Entries.Clear();
        foreach (var entry in await _services.Journal.GetAllAsync())
            Entries.Add(entry);
    }

    private void OpenSelected()
    {
        if (Selected is null || !File.Exists(Selected.FilePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo(Selected.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _report("Не вдалося відкрити файл: " + ex.Message);
        }
    }
}
