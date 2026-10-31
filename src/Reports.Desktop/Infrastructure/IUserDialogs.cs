using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Reports.Desktop.Infrastructure;

/// <summary>Діалоги, потрібні моделям подання; окремий інтерфейс дає змогу перевіряти моделі без вікон.</summary>
public interface IUserDialogs
{
    /// <summary>Запитує підтвердження (так/ні).</summary>
    bool Confirm(string title, string message);

    /// <summary>Запитує шлях для збереження файлу; null, якщо користувач скасував.</summary>
    string? PickSaveFile(string suggestedName, string filter);
}

/// <summary>Діалоги на стандартних вікнах Windows.</summary>
public sealed class WpfUserDialogs : IUserDialogs
{
    /// <inheritdoc/>
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            == MessageBoxResult.Yes;

    /// <inheritdoc/>
    public string? PickSaveFile(string suggestedName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
