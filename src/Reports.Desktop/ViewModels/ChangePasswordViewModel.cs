using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;
using Reports.Desktop.Infrastructure;
using Reports.Identity;

namespace Reports.Desktop.ViewModels;

/// <summary>Зміна власного пароля (FR-27) для поточного користувача; без типів вікон.</summary>
public sealed class ChangePasswordViewModel : ObservableObject
{
    private readonly ChangePasswordService _service;
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly AccessGuard _guard;

    private string _current = string.Empty;
    private string _new = string.Empty;
    private string _confirm = string.Empty;
    private string _error = string.Empty;

    public ChangePasswordViewModel(
        ChangePasswordService service, IAccountRepository accounts, IPasswordHasher hasher, AccessGuard guard)
    {
        _service = service;
        _accounts = accounts;
        _hasher = hasher;
        _guard = guard;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync);
    }

    /// <summary>Пароль успішно змінено.</summary>
    public event EventHandler? Changed;

    public AsyncRelayCommand SubmitCommand { get; }

    public string CurrentPassword
    {
        get => _current;
        set => Set(ref _current, value ?? string.Empty);
    }

    public string NewPassword
    {
        get => _new;
        set => Set(ref _new, value ?? string.Empty);
    }

    public string ConfirmPassword
    {
        get => _confirm;
        set => Set(ref _confirm, value ?? string.Empty);
    }

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    /// <summary>Змінює пароль; помилки політики показує українським текстом.</summary>
    public async Task SubmitAsync()
    {
        Error = string.Empty;
        try
        {
            var session = _guard.Demand(AccessActions.ChangeOwnPassword);

            if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
            {
                Error = "Пароль і підтвердження не збігаються.";
                return;
            }

            var account = await _accounts.FindByLoginAsync(Account.NormalizeLogin(session.Login))
                ?? throw new DomainRuleViolationException("Account not found.");
            await _service.ChangePasswordAsync(_accounts, _hasher, account, CurrentPassword, NewPassword);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is DomainRuleViolationException or AccessDeniedException)
        {
            Error = UiText.Describe(ex);
        }
    }
}
