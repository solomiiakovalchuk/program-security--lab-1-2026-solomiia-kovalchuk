using System.Windows;
using System.Windows.Controls;
using Reports.Desktop.ViewModels;

namespace Reports.Desktop.Views;

/// <summary>Вкладка «Облікові записи»; код лише переносить паролі з PasswordBox у модель.</summary>
public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is AccountsViewModel old) old.PasswordsCleared -= OnCleared;
            if (e.NewValue is AccountsViewModel now) now.PasswordsCleared += OnCleared;
        };
    }

    private AccountsViewModel? Model => DataContext as AccountsViewModel;

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Model is { } m) m.NewPassword = NewPasswordBox.Password;
    }

    private void OnTemporaryPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Model is { } m) m.TemporaryPassword = TemporaryPasswordBox.Password;
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        NewPasswordBox.Clear();
        TemporaryPasswordBox.Clear();
    }
}
