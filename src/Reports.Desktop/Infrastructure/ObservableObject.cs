using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reports.Desktop.Infrastructure;

/// <summary>
/// Базовий клас моделей подання: сповіщає інтерфейс про зміну властивостей.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Присвоює значення й сповіщає інтерфейс, якщо воно справді змінилося.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
