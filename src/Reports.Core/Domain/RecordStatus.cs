namespace Reports.Core.Domain;

/// <summary>
/// Статус запису у журналі відповідей. Помилкова подача не видаляється,
/// а позначається як <see cref="Cancelled"/> — слід залишається,
/// звітність не змінюється заднім числом непомітно.
/// </summary>
public enum RecordStatus
{
    /// <summary>Запис іде в рапорт.</summary>
    Active = 0,

    /// <summary>Запис ігнорується назавжди (колонка «Статус» = СКАСОВАНО).</summary>
    Cancelled = 1,

    /// <summary>Значення не знайдено в довіднику — запис потребує ручного розбору.</summary>
    NeedsAttention = 2
}

public static class RecordStatusExtensions
{
    public const string CancelledMarker = "СКАСОВАНО";
    public const string NeedsAttentionMarker = "ПОТРЕБУЄ УВАГИ";

    public static RecordStatus Parse(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        null or "" => RecordStatus.Active,
        CancelledMarker => RecordStatus.Cancelled,
        NeedsAttentionMarker => RecordStatus.NeedsAttention,
        _ => RecordStatus.Active
    };

    public static string ToMarker(this RecordStatus status) => status switch
    {
        RecordStatus.Cancelled => CancelledMarker,
        RecordStatus.NeedsAttention => NeedsAttentionMarker,
        _ => string.Empty
    };

    public static string ToDisplay(this RecordStatus status) => status switch
    {
        RecordStatus.Cancelled => "Скасовано",
        RecordStatus.NeedsAttention => "Потребує уваги",
        _ => "Активний"
    };
}
