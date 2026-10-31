using System.Windows;

namespace Reports.Desktop.Infrastructure;

/// <summary>
/// Місток для прив'язок, які не бачать візуального дерева — зокрема
/// заголовків стовпців DataGrid: колонки не є частиною дерева, тому
/// RelativeSource до них не працює. Проксі кладеться в ресурси й несе DataContext далі.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy),
            new UIPropertyMetadata(null));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
