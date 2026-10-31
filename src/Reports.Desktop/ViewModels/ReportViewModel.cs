using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Reports.Core.Aggregation;
using Reports.Core.Domain;
using Reports.Core.Periods;
using Reports.Core.Services;
using Reports.Desktop.Infrastructure;

namespace Reports.Desktop.ViewModels;

/// <summary>
/// Вікно формування рапорту: період, підписант, фільтри, наказ,
/// попередній перегляд тексту й створення документа.
/// </summary>
public sealed class ReportViewModel : ObservableObject
{
    /// <summary>Позначка «фільтр не застосовується».</summary>
    public const string AnyOption = "— усі —";

    private readonly AppServices _services;
    private readonly Action<string> _report;

    private DateTime _from = DateTime.Today;
    private DateTime _to = DateTime.Today;
    private Person? _signer;
    private string _operatorFilter = AnyOption;
    private object? _settlementFilter;
    private string _order = string.Empty;
    private string _previewText = string.Empty;
    private string _lastFilePath = string.Empty;

    public ReportViewModel(AppServices services, Action<string> report)
    {
        _services = services;
        _report = report;
        _order = services.Settings.OrderDefault;

        ApplyPresetCommand = new RelayCommand(p =>
        {
            if (p is PeriodPreset preset) ApplyPreset(preset);
        });

        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => Signer is not null);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, () => Signer is not null);
        OpenFileCommand = new RelayCommand(OpenLastFile, () => File.Exists(_lastFilePath));
        OpenFolderCommand = new RelayCommand(OpenOutputFolder);

        ApplyPreset(new LastSevenDaysPreset());
    }

    public IReadOnlyList<PeriodPreset> Presets => PeriodPreset.All;

    public ObservableCollection<Person> Signers { get; } = new();
    public ObservableCollection<string> Operators { get; } = new();
    public ObservableCollection<object> Settlements { get; } = new();

    public DateTime From
    {
        get => _from;
        set { if (Set(ref _from, value)) PreviewText = string.Empty; }
    }

    public DateTime To
    {
        get => _to;
        set { if (Set(ref _to, value)) PreviewText = string.Empty; }
    }

    public Person? Signer
    {
        get => _signer;
        set => Set(ref _signer, value);
    }

    public string OperatorFilter
    {
        get => _operatorFilter;
        set => Set(ref _operatorFilter, value);
    }

    public object? SettlementFilter
    {
        get => _settlementFilter;
        set => Set(ref _settlementFilter, value);
    }

    public string Order
    {
        get => _order;
        set => Set(ref _order, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => Set(ref _previewText, value);
    }

    public string PeriodSummary => $"{From:dd.MM.yyyy} — {To:dd.MM.yyyy}";

    public RelayCommand ApplyPresetCommand { get; }
    public AsyncRelayCommand PreviewCommand { get; }
    public AsyncRelayCommand GenerateCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public void ApplyPreset(PeriodPreset preset)
    {
        var period = preset.Now();
        From = period.From;
        To = period.To;
        OnPropertyChanged(nameof(PeriodSummary));
    }

    /// <summary>Перечитує довідники: підписанти, оператори, населені пункти.</summary>
    public async Task LoadAsync()
    {
        var previousSigner = Signer?.Email;

        Signers.Clear();
        foreach (var person in await _services.People.GetAllAsync())
            Signers.Add(person);

        Settlements.Clear();
        Settlements.Add(AnyOption);
        foreach (var settlement in await _services.References.GetSettlementsAsync())
            Settlements.Add(settlement);
        SettlementFilter ??= Settlements[0];

        var missions = await _services.Missions.GetAllAsync();
        Operators.Clear();
        Operators.Add(AnyOption);
        foreach (var email in missions.Select(m => m.OperatorEmail)
                                      .Where(e => !string.IsNullOrWhiteSpace(e))
                                      .Distinct(StringComparer.OrdinalIgnoreCase)
                                      .OrderBy(e => e))
            Operators.Add(email);

        if (!Operators.Contains(OperatorFilter)) OperatorFilter = AnyOption;

        Signer = Signers.FirstOrDefault(p => p.Email == previousSigner)
              ?? Signers.FirstOrDefault(p => p.Email == _services.Settings.DefaultSignerEmail)
              ?? Signers.FirstOrDefault();
    }

    private ReportRequest BuildRequest() => new()
    {
        Period = DatePeriod.Of(From, To),
        Signer = Signer!,
        OperatorEmail = OperatorFilter == AnyOption ? null : OperatorFilter,
        SettlementId = (SettlementFilter as Settlement)?.Id,
        Order = Order,
        Subunit = _services.Settings.SubunitText,
        Format = ReportFormat.Word
    };

    private async Task PreviewAsync()
    {
        try
        {
            var content = await _services.Reports.PreviewAsync(BuildRequest());

            if (content.IsEmpty)
            {
                PreviewText = "За вказаний період записів немає.\n\n" +
                              "Перевірте межі періоду та фільтри, або синхронізуйте дані " +
                              "у вкладці «Відповіді».";
                _report("Прев'ю: записів немає.");
                return;
            }

            PreviewText = content.BuildSentence();
            _report($"Прев'ю сформовано: подач — {content.MissionCount}, " +
                    $"епізодів — {content.EngagementCount}.");
        }
        catch (ReportException ex)
        {
            PreviewText = ex.Message;
            _report("Помилка: " + ex.Message);
        }
    }

    private async Task GenerateAsync()
    {
        try
        {
            var path = await _services.Reports.GenerateAsync(BuildRequest());
            _lastFilePath = path;

            PreviewText = $"Документ створено:\n{path}";
            _report("Рапорт створено: " + Path.GetFileName(path));
        }
        catch (ReportException ex)
        {
            PreviewText = ex.Message;
            _report("Помилка: " + ex.Message);
        }
        catch (Exception ex)
        {
            PreviewText = "Не вдалося створити документ: " + ex.Message;
            _report("Помилка створення документа.");
        }
    }

    private void OpenLastFile()
    {
        if (File.Exists(_lastFilePath)) OpenInShell(_lastFilePath);
    }

    private void OpenOutputFolder()
    {
        var folder = _services.Settings.OutputFolder;
        Directory.CreateDirectory(folder);
        OpenInShell(folder);
    }

    private void OpenInShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _report("Не вдалося відкрити: " + ex.Message);
        }
    }
}
