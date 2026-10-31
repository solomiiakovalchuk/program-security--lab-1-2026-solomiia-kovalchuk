using System.Collections.ObjectModel;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;
using Reports.Desktop.Infrastructure;
using Reports.Identity;

namespace Reports.Desktop.ViewModels;

/// <summary>Рядок списку облікових записів; ніколи не містить хешів чи солі.</summary>
public sealed class AccountRow
{
    public AccountRow(AccountInfo info)
    {
        Info = info;
    }

    public AccountInfo Info { get; }
    public Guid Id => Info.Id;
    public string Login => Info.Login;
    public Role Role => Info.Role;
    public string RoleText => UiText.RoleName(Info.Role) + (Info.IsBuiltIn ? " (вбудований)" : string.Empty);
    public bool IsBlocked => Info.IsBlocked;
    public string BlockedText => Info.IsBlocked ? "Так" : "Ні";
    public string MustChangeText => !Info.HasPassword || Info.MustChangePassword ? "Так" : "Ні";

    public string LockedText =>
        Info.LockedUntilUtc is { } until && until > DateTime.UtcNow
            ? until.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : string.Empty;
}

/// <summary>Вибір ролі у списку.</summary>
public sealed record RoleOption(Role Role, string Name);

/// <summary>Вкладка «Облікові записи» (FR-28): лише для адміністратора.</summary>
public sealed class AccountsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly Action<string> _report;
    private readonly IUserDialogs _dialogs;

    private AccountRow? _selected;
    private string _newLogin = string.Empty;
    private string _newPassword = string.Empty;
    private string _temporaryPassword = string.Empty;
    private RoleOption _newRole;
    private RoleOption? _selectedRole;
    private string _error = string.Empty;

    public AccountsViewModel(AppServices services, Action<string> report, IUserDialogs dialogs)
    {
        _services = services;
        _report = report;
        _dialogs = dialogs;

        Roles = new[]
        {
            new RoleOption(Role.Operator, UiText.RoleName(Role.Operator)),
            new RoleOption(Role.Administrator, UiText.RoleName(Role.Administrator))
        };
        _newRole = Roles[0];

        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        BlockCommand = new AsyncRelayCommand(BlockAsync, () => Selected is { IsBlocked: false });
        UnblockCommand = new AsyncRelayCommand(UnblockAsync, () => Selected is { IsBlocked: true });
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => Selected is not null);
        ChangeRoleCommand = new AsyncRelayCommand(ChangeRoleAsync, () => Selected is not null && SelectedRole is not null);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => Selected is not null);
    }

    /// <summary>Потрібно очистити поля паролів у поданні.</summary>
    public event EventHandler? PasswordsCleared;

    public ObservableCollection<AccountRow> Accounts { get; } = new();
    public RoleOption[] Roles { get; }

    public AsyncRelayCommand ReloadCommand { get; }
    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand BlockCommand { get; }
    public AsyncRelayCommand UnblockCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand ChangeRoleCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }

    public AccountRow? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;
            SelectedRole = value is null ? null : Roles.First(r => r.Role == value.Role);
        }
    }

    public RoleOption? SelectedRole
    {
        get => _selectedRole;
        set => Set(ref _selectedRole, value);
    }

    public string NewLogin
    {
        get => _newLogin;
        set => Set(ref _newLogin, value ?? string.Empty);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => Set(ref _newPassword, value ?? string.Empty);
    }

    public RoleOption NewRole
    {
        get => _newRole;
        set { if (value is not null) Set(ref _newRole, value); }
    }

    public string TemporaryPassword
    {
        get => _temporaryPassword;
        set => Set(ref _temporaryPassword, value ?? string.Empty);
    }

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    /// <summary>Перечитує список облікових записів.</summary>
    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var selectedId = Selected?.Id;
            var list = await _services.AccountManagement.ListAccountsAsync();
            Accounts.Clear();
            foreach (var info in list) Accounts.Add(new AccountRow(info));
            Selected = selectedId is null ? null : Accounts.FirstOrDefault(a => a.Id == selectedId);
        }, null);
    }

    /// <summary>Створює обліковий запис із початковим паролем (зміна обов'язкова при першому вході).</summary>
    public async Task CreateAsync()
    {
        await RunAsync(async () =>
        {
            var info = await _services.AccountManagement.CreateAccountAsync(NewLogin, NewRole.Role, NewPassword);
            NewLogin = string.Empty;
            NewPassword = string.Empty;
            PasswordsCleared?.Invoke(this, EventArgs.Empty);
            await ReloadKeepingAsync(info.Id);
        }, "Обліковий запис створено. Користувач має змінити пароль при першому вході.");
    }

    /// <summary>Блокує вибраний обліковий запис.</summary>
    public async Task BlockAsync()
    {
        if (Selected is not { } row) return;
        await RunAsync(async () =>
        {
            await _services.AccountManagement.BlockAccountAsync(row.Id);
            await ReloadKeepingAsync(row.Id);
        }, $"Обліковий запис «{row.Login}» заблоковано.");
    }

    /// <summary>Розблоковує вибраний обліковий запис і знімає тимчасове блокування.</summary>
    public async Task UnblockAsync()
    {
        if (Selected is not { } row) return;
        await RunAsync(async () =>
        {
            await _services.AccountManagement.UnblockAccountAsync(row.Id);
            await ReloadKeepingAsync(row.Id);
        }, $"Обліковий запис «{row.Login}» розблоковано.");
    }

    /// <summary>Видаляє вибраний обліковий запис після підтвердження.</summary>
    public async Task DeleteAsync()
    {
        if (Selected is not { } row) return;
        if (!_dialogs.Confirm("Видалення облікового запису", $"Видалити обліковий запис «{row.Login}»? Дію не можна скасувати."))
            return;

        await RunAsync(async () =>
        {
            await _services.AccountManagement.DeleteAccountAsync(row.Id);
            Selected = null;
            await LoadInnerAsync();
        }, $"Обліковий запис «{row.Login}» видалено.");
    }

    /// <summary>Змінює роль вибраного облікового запису.</summary>
    public async Task ChangeRoleAsync()
    {
        if (Selected is not { } row || SelectedRole is not { } role) return;
        await RunAsync(async () =>
        {
            await _services.AccountManagement.ChangeRoleAsync(row.Id, role.Role);
            await ReloadKeepingAsync(row.Id);
        }, $"Роль облікового запису «{row.Login}»: {role.Name}.");
    }

    /// <summary>Задає тимчасовий пароль; користувач змінить його при наступному вході.</summary>
    public async Task ResetPasswordAsync()
    {
        if (Selected is not { } row) return;
        await RunAsync(async () =>
        {
            await _services.AccountManagement.ResetPasswordAsync(row.Id, TemporaryPassword);
            TemporaryPassword = string.Empty;
            PasswordsCleared?.Invoke(this, EventArgs.Empty);
            await ReloadKeepingAsync(row.Id);
        }, $"Тимчасовий пароль для «{row.Login}» задано; його треба змінити при наступному вході.");
    }

    private async Task ReloadKeepingAsync(Guid id)
    {
        await LoadInnerAsync();
        Selected = Accounts.FirstOrDefault(a => a.Id == id);
    }

    private async Task LoadInnerAsync()
    {
        var list = await _services.AccountManagement.ListAccountsAsync();
        Accounts.Clear();
        foreach (var info in list) Accounts.Add(new AccountRow(info));
    }

    private async Task RunAsync(Func<Task> action, string? success)
    {
        Error = string.Empty;
        try
        {
            await action();
            if (success is not null) _report(success);
        }
        catch (Exception ex) when (ex is DomainRuleViolationException or AccessDeniedException)
        {
            Error = UiText.Describe(ex);
            _report(Error);
        }
    }
}
