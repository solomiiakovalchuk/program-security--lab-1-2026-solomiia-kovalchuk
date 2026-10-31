using System.Windows;
using Reports.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Reports.Desktop.Views;

/// <summary>Вікно входу. Код тут лише переносить паролі з PasswordBox у модель подання.</summary>
public partial class LoginWindow : FluentWindow
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is LoginViewModel old)
            {
                old.PasswordsCleared -= OnPasswordsCleared;
                old.CredentialPasswordCleared -= OnCredentialCleared;
            }

            if (e.NewValue is LoginViewModel now)
            {
                now.PasswordsCleared += OnPasswordsCleared;
                now.CredentialPasswordCleared += OnCredentialCleared;
            }
        };
        Loaded += (_, _) => LoginBox.Focus();
    }

    /// <summary>True, якщо вікно закрито після успішного входу.</summary>
    public bool Succeeded { get; set; }

    private LoginViewModel? Model => DataContext as LoginViewModel;

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Model is { } m) m.Password = PasswordBox.Password;
    }

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Model is { } m) m.NewPassword = NewPasswordBox.Password;
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Model is { } m) m.ConfirmPassword = ConfirmPasswordBox.Password;
    }

    private void OnPasswordsCleared(object? sender, EventArgs e)
    {
        NewPasswordBox.Clear();
        ConfirmPasswordBox.Clear();
    }

    private void OnCredentialCleared(object? sender, EventArgs e) => PasswordBox.Clear();
}
