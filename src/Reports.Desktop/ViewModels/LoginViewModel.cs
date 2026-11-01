using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;
using Reports.Desktop.Infrastructure;
using Reports.Identity;

namespace Reports.Desktop.ViewModels;

/// <summary>Крок вікна входу.</summary>
public enum LoginStep
{
    /// <summary>Логін і пароль.</summary>
    Credentials,

    /// <summary>Задати перший пароль (вбудований ADMIN).</summary>
    InitialPassword,

    /// <summary>Обов'язкова зміна пароля після входу.</summary>
    ChangePassword
}

/// <summary>
/// Логіка вікна входу без жодних типів вікон: вхід, початковий пароль, примусова зміна.
/// Усі відмови входу показуються одним і тим самим текстом (UniformFailureDPolicy).
/// </summary>
public sealed class LoginViewModel : ObservableObject
{
    private readonly AuthenticationService _authentication;
    private readonly ChangePasswordService _changePassword;
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;

    private LoginStep _step = LoginStep.Credentials;
    private string _login = string.Empty;
    private string _password = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _error = string.Empty;
    private bool _busy;

    public LoginViewModel(
        AuthenticationService authentication,
        ChangePasswordService changePassword,
        IAccountRepository accounts,
        IPasswordHasher hasher,
        bool integrityWarning = false)
    {
        _authentication = authentication;
        _changePassword = changePassword;
        _accounts = accounts;
        _hasher = hasher;
        IntegrityWarning = integrityWarning;

        SubmitCommand = new AsyncRelayCommand(SubmitAsync);
        BackCommand = new AsyncRelayCommand(BackAsync);
    }

    /// <summary>Викликається, коли вхід завершено й сесія створена.</summary>
    public event EventHandler? LoggedIn;

    /// <summary>Викликається, коли треба очистити поля нового пароля й підтвердження у вікні.</summary>
    public event EventHandler? PasswordsCleared;

    /// <summary>Викликається, коли треба очистити поле пароля на кроці входу.</summary>
    public event EventHandler? CredentialPasswordCleared;

    public bool IntegrityWarning { get; }

    public string IntegrityWarningText => "Збірку не підписано (режим розробника).";

    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    public LoginStep Step
    {
        get => _step;
        private set
        {
            if (!Set(ref _step, value)) return;
            OnPropertyChanged(nameof(IsCredentialsStep));
            OnPropertyChanged(nameof(IsPasswordStep));
            OnPropertyChanged(nameof(IsInitialStep));
            OnPropertyChanged(nameof(Heading));
            OnPropertyChanged(nameof(SubmitText));
        }
    }

    public bool IsCredentialsStep => _step == LoginStep.Credentials;
    public bool IsPasswordStep => _step != LoginStep.Credentials;
    public bool IsInitialStep => _step == LoginStep.InitialPassword;

    public string Heading => _step switch
    {
        LoginStep.InitialPassword => "Задайте пароль адміністратора",
        LoginStep.ChangePassword => "Змініть пароль, щоб продовжити",
        _ => "Вхід до системи"
    };

    public string SubmitText => _step == LoginStep.Credentials ? "Увійти" : "Зберегти пароль";

    public string Login
    {
        get => _login;
        set => Set(ref _login, value ?? string.Empty);
    }

    public string Password
    {
        get => _password;
        set => Set(ref _password, value ?? string.Empty);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => Set(ref _newPassword, value ?? string.Empty);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => Set(ref _confirmPassword, value ?? string.Empty);
    }

    public string Error
    {
        get => _error;
        private set => Set(ref _error, value);
    }

    /// <summary>Виконує дію поточного кроку.</summary>
    public async Task SubmitAsync()
    {
        if (_busy) return;
        _busy = true;
        Error = string.Empty;
        try
        {
            switch (_step)
            {
                case LoginStep.Credentials: await SignInAsync(); break;
                case LoginStep.InitialPassword: await SetInitialPasswordAsync(); break;
                default: await ChangePasswordStepAsync(); break;
            }
        }
        catch (DomainRuleViolationException ex)
        {
            Error = UiText.Translate(ex.Message);
        }
        catch (Exception)
        {
            // Деталі не розкриваємо; службова помилка на вході виглядає як відмова входу.
            Error = _step == LoginStep.Credentials ? UiText.LoginFailed : "Не вдалося зберегти пароль. Спробуйте ще раз.";
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Повертає до кроку логіна; якщо сесію вже відкрито (примусова зміна), закриває її.</summary>
    public async Task BackAsync()
    {
        if (_step == LoginStep.ChangePassword)
            await _authentication.LogoutAsync();

        Step = LoginStep.Credentials;
        ClearCredentialPassword();
        ClearNewPasswords();
        Error = string.Empty;
    }

    /// <summary>Скидає форму до початкового стану перед повторним показом вікна.</summary>
    public void Reset()
    {
        Step = LoginStep.Credentials;
        Login = string.Empty;
        ClearCredentialPassword();
        ClearNewPasswords();
        Error = string.Empty;
    }

    private async Task SignInAsync()
    {
        var outcome = await _authentication.LoginAsync(Login, Password);
        switch (outcome)
        {
            case LoginOutcome.Success:
                Finish();
                break;
            case LoginOutcome.RequiresInitialPassword:
                Step = LoginStep.InitialPassword;
                ClearNewPasswords();
                break;
            case LoginOutcome.RequiresPasswordChange:
                Step = LoginStep.ChangePassword;
                ClearNewPasswords();
                break;
            default:
                ClearCredentialPassword();
                Error = UiText.LoginFailed;
                break;
        }
    }

    private async Task SetInitialPasswordAsync()
    {
        if (!ConfirmationMatches()) return;

        var account = await FindAccountAsync();
        await _changePassword.SetInitialPasswordAsync(_accounts, _hasher, account, NewPassword);

        // Пароль задано: входимо з ним, щоб створити сесію й зафіксувати вхід в аудиті.
        var outcome = await _authentication.LoginAsync(Login, NewPassword);
        if (outcome == LoginOutcome.Success || outcome == LoginOutcome.RequiresPasswordChange)
            Finish();
        else
            await BackAsync();
    }

    private async Task ChangePasswordStepAsync()
    {
        if (!ConfirmationMatches()) return;

        var account = await FindAccountAsync();
        await _changePassword.ChangePasswordAsync(_accounts, _hasher, account, Password, NewPassword);
        Finish();
    }

    private bool ConfirmationMatches()
    {
        if (string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal)) return true;
        Error = "Пароль і підтвердження не збігаються.";
        return false;
    }

    private async Task<Account> FindAccountAsync() =>
        await _accounts.FindByLoginAsync(Account.NormalizeLogin(Login))
        ?? throw new DomainRuleViolationException("Account not found.");

    private void Finish()
    {
        ClearCredentialPassword();
        ClearNewPasswords();
        LoggedIn?.Invoke(this, EventArgs.Empty);
    }

    private void ClearCredentialPassword()
    {
        Password = string.Empty;
        CredentialPasswordCleared?.Invoke(this, EventArgs.Empty);
    }

    private void ClearNewPasswords()
    {
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        PasswordsCleared?.Invoke(this, EventArgs.Empty);
    }
}
