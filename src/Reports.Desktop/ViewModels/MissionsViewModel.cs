using System.Collections.ObjectModel;
using Reports.Core.Domain;
using Reports.Core.Identity;
using Reports.Integration;
using Reports.Desktop.Infrastructure;

namespace Reports.Desktop.ViewModels;

/// <summary>Рядок таблиці відповідей у зручному для перегляду вигляді.</summary>
public sealed class MissionRow : ObservableObject
{
    private readonly Mission _mission;

    public MissionRow(Mission mission) => _mission = mission;

    public Mission Source => _mission;

    public int Id => _mission.Id;
    public DateTime Date => _mission.Date;
    public string Operator => _mission.OperatorEmail;
    public string Settlement => _mission.Settlement?.ShortName ?? "—";
    public string Uav => string.Join(", ", _mission.Targets.Select(t => t.UavType?.ShortName ?? "?"));
    public int UavCount => _mission.TotalUavCount;
    public string Targets => string.Join(", ", _mission.Targets.Select(t => t.TargetType?.ShortName ?? "?"));

    public string Munitions => string.Join(", ", _mission.Targets
        .SelectMany(t => t.Munitions)
        .Select(m => m.MunitionType?.ShortName)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Distinct());

    public string Status => _mission.Status.ToDisplay();

    public bool IsCancelled => _mission.Status == RecordStatus.Cancelled;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsCancelled));
    }
}

/// <summary>
/// Вкладка «Відповіді»: синхронізація з джерелом, перегляд поданих
/// епізодів і скасування помилкових записів.
/// </summary>
public sealed class MissionsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly Action<string> _report;

    private MissionRow? _selected;
    private string _summary = "Дані ще не завантажені.";

    public MissionsViewModel(AppServices services, Action<string> report)
    {
        _services = services;
        _report = report;

        SyncCommand = new AsyncRelayCommand(SyncAsync);
        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        CancelCommand = new AsyncRelayCommand(ToggleCancelAsync, () => Selected is not null);
        PublishCommand = new AsyncRelayCommand(PublishAsync, () => !services.Settings.UseDemoData && services.Guard.IsAllowed(AccessActions.PublishToGoogle));
    }

    public ObservableCollection<MissionRow> Rows { get; } = new();

    public MissionRow? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public AsyncRelayCommand SyncCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand PublishCommand { get; }

    /// <summary>Подія для головної моделі: дані змінилися, оновіть інші вкладки.</summary>
    public event EventHandler? DataChanged;

    public async Task LoadAsync()
    {
        Rows.Clear();
        foreach (var mission in await _services.Missions.GetAllAsync())
            Rows.Add(new MissionRow(mission));

        var active = Rows.Count(r => !r.IsCancelled);
        Summary = Rows.Count == 0
            ? "Записів немає. Натисніть «Синхронізувати», щоб завантажити дані."
            : $"Усього записів: {Rows.Count}, з них активних: {active}.";
    }

    private async Task SyncAsync()
    {
        try
        {
            var source = _services.CreateResponseSource();
            _report($"Синхронізація ({source.Title})…");

            var result = await _services.Sync.SyncAsync(source, !_services.Settings.UseDemoData);

            await LoadAsync();
            Summary = result.Describe();
            _report($"Синхронізація завершена: імпортовано {result.Imported}.");
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (GoogleAccessException ex)
        {
            Summary = ex.Message;
            _report("Google: " + ex.Message.Split('\n')[0]);
        }
        catch (Exception ex)
        {
            Summary = "Не вдалося синхронізувати: " + ex.Message;
            _report("Помилка синхронізації.");
        }
    }

    /// <summary>
    /// Зворотний запис: дописує в таблицю нові довідники, людей і статуси.
    /// Викликається окремою дією, а не після кожної правки — щоб запис
    /// у спільний документ був передбачуваним.
    /// </summary>
    private async Task PublishAsync()
    {
        var sink = _services.CreateResponseSink();
        if (sink is null)
        {
            Summary = "У демонстраційному режимі вивантажувати нікуди. " +
                      "Вимкніть демонстраційні дані у налаштуваннях.";
            return;
        }

        try
        {
            _report($"Вивантаження ({sink.Title})…");
            var result = await _services.Publish.PublishAsync(sink);

            Summary = result.Describe();
            _report("Вивантаження завершено.");
        }
        catch (GoogleAccessException ex)
        {
            Summary = ex.Message;
            _report("Google: не вдалося вивантажити.");
        }
        catch (Exception ex)
        {
            Summary = "Не вдалося вивантажити: " + ex.Message;
            _report("Помилка вивантаження.");
        }
    }

    private async Task ToggleCancelAsync()
    {
        if (Selected is null) return;

        // Помилкова подача не видаляється — вона позначається скасованою (з перевіркою ролі й аудитом).
        var mission = await _services.MissionStatus.ToggleAsync(Selected.Id);
        if (mission is null) return;

        _report(mission.Status == RecordStatus.Cancelled
            ? $"Запис від {mission.Date:dd.MM.yyyy} скасовано."
            : $"Запис від {mission.Date:dd.MM.yyyy} повернуто в рапорт.");

        await LoadAsync();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
