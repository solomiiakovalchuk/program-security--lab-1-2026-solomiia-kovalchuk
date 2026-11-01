using System.IO;
using System.Windows;
using System.Windows.Threading;
using Reports.Core.Identity.Exceptions;
using Reports.Desktop.Infrastructure;
using Reports.Integrity;
using Reports.Desktop.ViewModels;
using Reports.Desktop.Views;
using Wpf.Ui.Appearance;

namespace Reports.Desktop;

public partial class App : Application
{
    private readonly IUserDialogs _dialogs = new WpfUserDialogs();
    private AppServices? _services;
    private MainViewModel? _model;
    private MainWindow? _mainWindow;
    private bool _switchingUser;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Вікна приходять і йдуть (вхід → головне → вхід), тому закриваємо застосунок явно.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            // SEC-16: перед усім іншим перевіряємо підписаний маніфест випуску.
            var integrity = StartupIntegrityCheck.Run(AppContext.BaseDirectory, ReleaseKey.Load());

            if (integrity.Outcome == StartupIntegrityOutcome.Failed)
            {
                // Пояснення важливіше за журнал: подію пробуємо записати, але відмову показуємо в будь-якому разі
                // (наприклад, коли бракує самої збірки, потрібної для створення сервісів).
                try
                {
                    var failed = AppServices.Create(integrity);
                    await failed.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Log("Не вдалося записати подію порушення цілісності", ex);
                }

                MessageBox.Show(
                    "Цілісність застосунку порушена (" + integrity.Reason + "). " +
                    "Файли змінено або пошкоджено, тому запуск скасовано.\n\n" +
                    "Встановіть застосунок повторно з перевіреного джерела.",
                    "Перевірка цілісності",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var services = AppServices.Create(integrity);
            await services.InitializeAsync();

            _services = services;

            // SEC-14: підтвердження таблиці (назва й ідентифікатор) перед записом.
            services.ConfirmSpreadsheet = (title, id) =>
                Dispatcher.InvokeAsync(() => _dialogs.Confirm(
                    "Підтвердження таблиці",
                    $"Назва таблиці: {title}\nІдентифікатор: {id}\n\n" +
                    "Дані буде записано саме в цю таблицю. Підтвердити?")).Task;

            // Тема слідує за системною, поки користувач не перемкне вручну.
            ApplicationThemeManager.ApplySystemTheme();

            // Головне вікно створюється лише після успішного входу.
            ShowLogin();
        }
        catch (Exception ex)
        {
            Log("Помилка запуску", ex);
            MessageBox.Show(
                "Не вдалося запустити застосунок:\n\n" + ex.Message +
                "\n\nПодробиці у файлі error.log у теці даних.",
                "Помилка запуску",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ShowLogin()
    {
        var services = _services!;
        var login = new LoginViewModel(
            services.Authentication, services.ChangePassword, services.Accounts,
            services.PasswordHasher, services.IntegrityWarning);
        var window = new LoginWindow { DataContext = login };

        login.LoggedIn += (_, _) =>
        {
            window.Succeeded = true;
            window.Close();
        };

        window.Closed += async (_, _) =>
        {
            if (!window.Succeeded)
            {
                // Закрили вікно входу без входу — застосунок завершується.
                Shutdown();
                return;
            }

            await OpenMainAsync();
        };

        SystemThemeWatcher.Watch(window);
        window.Show();
    }

    private async Task OpenMainAsync()
    {
        try
        {
            var services = _services!;
            var model = new MainViewModel(services, _dialogs);
            model.LogoutRequested += (_, _) => Logout();
            model.ChangePasswordRequested += (_, _) => ShowChangePassword(model);
            await model.LoadAsync();

            var window = new MainWindow { DataContext = model };
            window.Closed += (_, _) =>
            {
                if (_switchingUser) return;
                services.SessionService.Logout();
                Shutdown();
            };

            _model = model;
            _mainWindow = window;
            MainWindow = window;
            SystemThemeWatcher.Watch(window);
            window.Show();
        }
        catch (Exception ex)
        {
            Log("Помилка відкриття головного вікна", ex);
            MessageBox.Show(
                "Не вдалося відкрити головне вікно:\n\n" + ex.Message,
                "Помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _services?.SessionService.Logout();
            Shutdown(1);
        }
    }

    private void Logout()
    {
        _switchingUser = true;
        try
        {
            _services!.SessionService.Logout();
            var window = _mainWindow;
            _model = null;
            _mainWindow = null;
            MainWindow = null;
            window?.Close();
        }
        finally
        {
            _switchingUser = false;
        }

        ShowLogin();
    }

    private void ShowChangePassword(MainViewModel model)
    {
        var services = _services!;
        var dialog = new ChangePasswordWindow(
            new ChangePasswordViewModel(services.ChangePassword, services.Accounts, services.PasswordHasher, services.Guard))
        {
            Owner = _mainWindow
        };
        if (dialog.ShowDialog() == true)
            model.SetStatus("Пароль змінено.");
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Відмова в доступі — штатна ситуація: показуємо її у рядку стану, без вікна помилки.
        if (e.Exception is AccessDeniedException denied && _model is not null)
        {
            _model.SetStatus(UiText.Describe(denied));
            e.Handled = true;
            return;
        }

        Log("Необроблена помилка", e.Exception);

        MessageBox.Show(
            "Сталася помилка:\n\n" + e.Exception.Message,
            "Помилка",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    /// <summary>
    /// Пише виняток у журнал помилок. Без цього діагностика збою на чужій
    /// машині зводиться до переказу тексту з екрана.
    /// </summary>
    private static void Log(string title, Exception exception)
    {
        try
        {
            var folder = AppDataFolder.Resolve();
            Directory.CreateDirectory(folder);

            File.AppendAllText(
                Path.Combine(folder, "error.log"),
                $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {title} ==={Environment.NewLine}" +
                exception + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
            // Журнал помилок сам по собі не має валити застосунок.
        }
    }
}
