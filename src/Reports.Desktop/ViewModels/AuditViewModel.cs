using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Reports.Audit;
using Reports.Core.Audit;
using Reports.Core.Identity.Exceptions;
using Reports.Desktop.Infrastructure;

namespace Reports.Desktop.ViewModels;

/// <summary>Рядок журналу аудиту для таблиці.</summary>
public sealed record AuditRow(string Time, string Actor, string Action, string Target, string Outcome, string Details);

/// <summary>Вкладка «Журнал аудиту» (FR-31, SEC-13): перегляд, фільтри, експорт, очищення старих записів.</summary>
public sealed class AuditViewModel : ObservableObject
{
    /// <summary>Значення фільтра «усі дії».</summary>
    public const string AllActions = "Усі дії";

    private const int PageSize = 50;

    private readonly AppServices _services;
    private readonly Action<string> _report;
    private readonly IUserDialogs _dialogs;

    private string _actorLogin = string.Empty;
    private string _action = AllActions;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private int _page;
    private int _total;
    private bool _chainBroken;
    private string _chainText = "Перевірка ланцюга цілісності…";
    private string _error = string.Empty;

    public AuditViewModel(AppServices services, Action<string> report, IUserDialogs dialogs)
    {
        _services = services;
        _report = report;
        _dialogs = dialogs;

        Actions = new[] { AllActions }
            .Concat(typeof(AuditAction)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .OrderBy(s => s, StringComparer.Ordinal))
            .ToArray();

        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        ApplyFiltersCommand = new AsyncRelayCommand(ApplyFiltersAsync);
        PreviousCommand = new AsyncRelayCommand(PreviousAsync, () => _page > 0);
        NextCommand = new AsyncRelayCommand(NextAsync, () => (_page + 1) * PageSize < _total);
        ExportCsvCommand = new AsyncRelayCommand(() => ExportAsync(AuditExportFormat.Csv));
        ExportJsonCommand = new AsyncRelayCommand(() => ExportAsync(AuditExportFormat.Json));
        RetentionCommand = new AsyncRelayCommand(ApplyRetentionAsync);
    }

    public ObservableCollection<AuditRow> Rows { get; } = new();
    public string[] Actions { get; }

    public AsyncRelayCommand ReloadCommand { get; }
    public AsyncRelayCommand ApplyFiltersCommand { get; }
    public AsyncRelayCommand PreviousCommand { get; }
    public AsyncRelayCommand NextCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ExportJsonCommand { get; }
    public AsyncRelayCommand RetentionCommand { get; }

    public string ActorLogin
    {
        get => _actorLogin;
        set => Set(ref _actorLogin, value ?? string.Empty);
    }

    public string SelectedAction
    {
        get => _action;
        set => Set(ref _action, value ?? AllActions);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set => Set(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => Set(ref _toDate, value);
    }

    /// <summary>True, якщо ланцюг хешів порушено: показується попередження адміністратору.</summary>
    public bool ChainBroken
    {
        get => _chainBroken;
        private set
        {
            if (Set(ref _chainBroken, value)) OnPropertyChanged(nameof(ChainIntact));
        }
    }

    public bool ChainIntact => !_chainBroken;

    public string ChainText
    {
        get => _chainText;
        private set => Set(ref _chainText, value);
    }

    public string PageText => _total == 0
        ? "Записів немає"
        : $"Записи {_page * PageSize + 1}–{Math.Min((_page + 1) * PageSize, _total)} із {_total}";

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    /// <summary>Перечитує поточну сторінку й заново перевіряє ланцюг хешів.</summary>
    public async Task LoadAsync()
    {
        Error = string.Empty;
        try
        {
            await VerifyChainAsync();

            var page = await _services.AuditQuery.QueryAsync(BuildQuery(_page * PageSize, PageSize));
            if (page.Total > 0 && page.Items.Count == 0 && _page > 0)
            {
                // Після очищення сторінка могла зникнути: повертаємось на останню.
                _page = (page.Total - 1) / PageSize;
                page = await _services.AuditQuery.QueryAsync(BuildQuery(_page * PageSize, PageSize));
            }

            _total = page.Total;
            Rows.Clear();
            foreach (var e in page.Items) Rows.Add(ToRow(e));
            OnPropertyChanged(nameof(PageText));
            CommandManagerRefresh();
        }
        catch (Exception ex) when (ex is AccessDeniedException or ArgumentException)
        {
            Fail(ex is AccessDeniedException ? UiText.Describe(ex) : "Некоректні фільтри: перевірте період.");
        }
    }

    /// <summary>Застосовує фільтри з першої сторінки.</summary>
    public Task ApplyFiltersAsync()
    {
        _page = 0;
        return LoadAsync();
    }

    /// <summary>Експортує записи за поточними фільтрами у файл.</summary>
    public async Task ExportAsync(AuditExportFormat format)
    {
        Error = string.Empty;
        var csv = format == AuditExportFormat.Csv;
        var path = _dialogs.PickSaveFile(
            $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.{(csv ? "csv" : "json")}",
            csv ? "CSV (*.csv)|*.csv" : "JSON (*.json)|*.json");
        if (path is null) return;

        try
        {
            var count = await _services.AuditExport.ExportToFileAsync(BuildQuery(0, 1), format, path);
            _report($"Експортовано записів: {count}. Файл: {path}");
            await LoadAsync();
        }
        catch (Exception ex) when (ex is AccessDeniedException or IOException or UnauthorizedAccessException
                                   or ArgumentException)
        {
            Fail(ex is AccessDeniedException ? UiText.Describe(ex) : "Не вдалося записати файл: " + ex.Message);
        }
    }

    /// <summary>Видаляє записи старші за період зберігання, але лише після експорту повного журналу.</summary>
    public async Task ApplyRetentionAsync()
    {
        Error = string.Empty;
        var days = _services.AuditRetention.RetentionDays;
        if (!_dialogs.Confirm(
                "Очищення журналу",
                $"Буде безповоротно видалено записи журналу, старші за {days} днів.\n\n" +
                "Перед очищенням потрібен експорт ПОВНОГО журналу (без фільтрів) у CSV або JSON. " +
                "Якщо такого експорту ще не було, очищення буде відхилено.\n\nПродовжити?"))
            return;

        try
        {
            var deleted = await _services.AuditRetention.ApplyAsync();
            _report($"Очищення виконано, видалено записів: {deleted}.");
            await LoadAsync();
        }
        catch (InvalidOperationException)
        {
            Fail("Очищення неможливе: спочатку експортуйте повний журнал (скиньте фільтри та оберіть експорт у CSV чи JSON).");
        }
        catch (AccessDeniedException ex)
        {
            Fail(UiText.Describe(ex));
        }
    }

    private async Task VerifyChainAsync()
    {
        var result = await _services.AuditQuery.VerifyChainAsync();
        ChainBroken = !result.IsIntact;
        ChainText = result.IsIntact
            ? $"Ланцюг цілісний (перевірено записів: {result.CheckedCount})."
            : "УВАГА: ланцюг хешів журналу порушено" +
              (result.FirstBrokenId is { } id ? $" (перший пошкоджений запис №{id})" : string.Empty) +
              ". Журнал могли змінити поза застосунком.";
    }

    private Task PreviousAsync()
    {
        if (_page > 0) _page--;
        return LoadAsync();
    }

    private Task NextAsync()
    {
        if ((_page + 1) * PageSize < _total) _page++;
        return LoadAsync();
    }

    private AuditQuery BuildQuery(int skip, int take)
    {
        DateTime? from = FromDate?.Date.ToUniversalTime();
        DateTime? to = ToDate?.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        var login = string.IsNullOrWhiteSpace(ActorLogin) ? null : ActorLogin.Trim();
        var action = _action == AllActions ? null : _action;
        return new AuditQuery(from, to, login, action, null, skip, take);
    }

    private static AuditRow ToRow(AuditEvent e) => new(
        e.OccurredAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
        e.ActorLogin,
        e.ActionType,
        string.IsNullOrEmpty(e.TargetId) ? e.TargetType.ToString() : $"{e.TargetType} {e.TargetId}",
        e.Outcome switch
        {
            AuditOutcome.Success => "Успіх",
            AuditOutcome.Failure => "Помилка",
            _ => "Відмовлено"
        },
        e.Details);

    private void Fail(string message)
    {
        Error = message;
        _report(message);
    }

    private static void CommandManagerRefresh() =>
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
}
