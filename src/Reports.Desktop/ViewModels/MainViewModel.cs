using System.Collections.ObjectModel;
using System.Windows;
using Reports.Core.Identity;
using Reports.Desktop.Infrastructure;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Reports.Desktop.ViewModels;

/// <summary>
/// Головна модель подання: бічна навігація, поточний вміст, рядок стану
/// й перемикання теми.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _status = "Готово.";
    private NavigationItem _selected = null!;
    private bool _isDark;

    public MainViewModel(AppServices services, IUserDialogs dialogs)
    {
        _services = services;

        Report = new ReportViewModel(services, SetStatus);
        Missions = new MissionsViewModel(services, SetStatus);
        Directories = new DirectoriesViewModel(services, SetStatus);
        Journal = new JournalViewModel(services, SetStatus);
        Settings = new SettingsViewModel(services, SetStatus);

        // Зміна даних в одній вкладці має бути видна в інших.
        Missions.DataChanged += async (_, _) => await RefreshDependentAsync();
        Directories.DataChanged += async (_, _) => await RefreshDependentAsync();

        Pages = new ObservableCollection<NavigationItem>
        {
            new("Формування рапорту", SymbolRegular.DocumentAdd24, Report),
            new("Відповіді", SymbolRegular.Table24, Missions),
            new("Довідники", SymbolRegular.BookQuestionMark24, Directories),
            new("Журнал", SymbolRegular.History24, Journal),
            new("Налаштування", SymbolRegular.Settings24, Settings)
        };

        // Вкладки адміністратора: створюються лише для нього (служби все одно перевіряють права).
        if (services.Guard.IsAllowed(AccessActions.ManageAccounts))
        {
            Accounts = new AccountsViewModel(services, SetStatus, dialogs);
            Pages.Add(new("Облікові записи", SymbolRegular.People24, Accounts));
        }

        if (services.Guard.IsAllowed(AccessActions.ViewAuditLog))
        {
            Audit = new AuditViewModel(services, SetStatus, dialogs);
            Pages.Add(new("Журнал аудиту", SymbolRegular.ShieldTask24, Audit));
        }
        _selected = Pages[0];

        _isDark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

        ReloadAllCommand = new AsyncRelayCommand(LoadAsync);
        SyncCommand = Missions.SyncCommand;
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        AboutCommand = new RelayCommand(ShowAbout);
        LogoutCommand = new RelayCommand(() => LogoutRequested?.Invoke(this, EventArgs.Empty));
        ChangePasswordCommand = new RelayCommand(() => ChangePasswordRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Користувач натиснув «Вийти».</summary>
    public event EventHandler? LogoutRequested;

    /// <summary>Користувач натиснув «Змінити пароль».</summary>
    public event EventHandler? ChangePasswordRequested;

    public AccountsViewModel? Accounts { get; }
    public AuditViewModel? Audit { get; }

    /// <summary>Підпис користувача: логін і роль.</summary>
    public string UserLabel
    {
        get
        {
            var session = _services.Session.Current;
            return session is null ? string.Empty : $"{session.Login} · {UiText.RoleName(session.Role)}";
        }
    }

    public RelayCommand LogoutCommand { get; }
    public RelayCommand ChangePasswordCommand { get; }

    public ReportViewModel Report { get; }
    public MissionsViewModel Missions { get; }
    public DirectoriesViewModel Directories { get; }
    public JournalViewModel Journal { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavigationItem> Pages { get; }

    public NavigationItem Selected
    {
        get => _selected;
        set { if (value is not null) Set(ref _selected, value); }
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public bool IsDark
    {
        get => _isDark;
        set { if (Set(ref _isDark, value)) ApplyTheme(); }
    }

    public string DataFolder => _services.DataFolder;

    public AsyncRelayCommand ReloadAllCommand { get; }
    public AsyncRelayCommand SyncCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand AboutCommand { get; }

    public void SetStatus(string message) => Status = message;

    public async Task LoadAsync()
    {
        await Directories.LoadAsync();
        await Missions.LoadAsync();
        await Report.LoadAsync();
        await Journal.LoadAsync();
        if (Accounts is not null) await Accounts.LoadAsync();
        if (Audit is not null) await Audit.LoadAsync();

        SetStatus($"{Settings.SourceDescription} Користувач: {UserLabel}.");
    }

    private async Task RefreshDependentAsync()
    {
        await Report.LoadAsync();
        await Journal.LoadAsync();
    }

    private void ToggleTheme() => IsDark = !IsDark;

    private void ApplyTheme() =>
        ApplicationThemeManager.Apply(
            _isDark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.Mica);

    private void ShowAbout()
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Про програму",
            Content =
                "Система формування рапортів про використання БпЛА та боєприпасів\n\n" +
                "Лабораторні роботи з дисципліни «Конструювання програмного забезпечення»\n" +
                "Хмельницький національний університет\n\n" +
                "C# / .NET 8 · WPF (MVVM) · WPF-UI (Fluent) · EF Core + SQLite ·\n" +
                "Google Sheets API · DocumentFormat.OpenXml\n\n" +
                $"Тека даних:\n{_services.DataFolder}",
            CloseButtonText = "Закрити"
        };
        _ = box.ShowDialogAsync();
    }
}
