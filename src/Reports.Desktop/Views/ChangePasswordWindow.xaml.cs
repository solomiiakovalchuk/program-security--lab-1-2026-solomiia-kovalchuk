using System.Windows;
using Reports.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace Reports.Desktop.Views;

/// <summary>Діалог зміни власного пароля (FR-27).</summary>
public partial class ChangePasswordWindow : FluentWindow
{
    public ChangePasswordWindow(ChangePasswordViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        model.Changed += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    private ChangePasswordViewModel Model => (ChangePasswordViewModel)DataContext;

    private void OnCurrentChanged(object sender, RoutedEventArgs e) => Model.CurrentPassword = ((System.Windows.Controls.PasswordBox)sender).Password;

    private void OnNewChanged(object sender, RoutedEventArgs e) => Model.NewPassword = ((System.Windows.Controls.PasswordBox)sender).Password;

    private void OnConfirmChanged(object sender, RoutedEventArgs e) => Model.ConfirmPassword = ((System.Windows.Controls.PasswordBox)sender).Password;
}
