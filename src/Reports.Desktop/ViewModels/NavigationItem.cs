using Reports.Desktop.Infrastructure;
using Wpf.Ui.Controls;

namespace Reports.Desktop.ViewModels;

/// <summary>
/// Пункт бічної навігації: заголовок, піктограма й модель подання,
/// яку показує права частина вікна при виборі пункту.
/// </summary>
public sealed class NavigationItem : ObservableObject
{
    public NavigationItem(string title, SymbolRegular icon, ObservableObject content)
    {
        Title = title;
        Icon = icon;
        Content = content;
    }

    public string Title { get; }

    public SymbolRegular Icon { get; }

    public ObservableObject Content { get; }
}
