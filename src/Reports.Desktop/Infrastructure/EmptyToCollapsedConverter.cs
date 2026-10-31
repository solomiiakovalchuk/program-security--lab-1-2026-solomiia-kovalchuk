using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Reports.Desktop.Infrastructure;

/// <summary>
/// Ховає елемент, поки прив'язаний рядок порожній. Потрібен, щоб панель
/// звіту про перевірку з'єднання не займала місце до першого запуску перевірки.
/// </summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
