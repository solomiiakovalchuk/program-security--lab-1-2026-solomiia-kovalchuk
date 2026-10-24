using Reports.Core.Domain;

namespace Reports.Core.Abstractions;

/// <summary>Довідникова позиція для вивантаження: ліва й права колонки блоку.</summary>
public readonly record struct DirectoryPair(string Left, string Right);

/// <summary>Нове значення колонки «Статус» для конкретного рядка-джерела.</summary>
public readonly record struct StatusChange(string SourceRowKey, string Marker);

/// <summary>Те, що застосунок хоче записати назад у зовнішню таблицю.</summary>
public sealed class PublishPayload
{
    public IReadOnlyList<DirectoryPair> Uav { get; init; } = Array.Empty<DirectoryPair>();
    public IReadOnlyList<DirectoryPair> Targets { get; init; } = Array.Empty<DirectoryPair>();
    public IReadOnlyList<DirectoryPair> Munitions { get; init; } = Array.Empty<DirectoryPair>();
    public IReadOnlyList<DirectoryPair> Settlements { get; init; } = Array.Empty<DirectoryPair>();
    public IReadOnlyList<Person> People { get; init; } = Array.Empty<Person>();
    public IReadOnlyList<StatusChange> Statuses { get; init; } = Array.Empty<StatusChange>();

    public bool IsEmpty =>
        Uav.Count == 0 && Targets.Count == 0 && Munitions.Count == 0 &&
        Settlements.Count == 0 && People.Count == 0 && Statuses.Count == 0;
}

/// <summary>Підсумок вивантаження для показу користувачеві.</summary>
public sealed record PublishResult(
    int DirectoriesAdded,
    int PeopleAdded,
    int StatusesUpdated,
    IReadOnlyList<string> Notes)
{
    public static PublishResult Nothing { get; } =
        new(0, 0, 0, new[] { "Нових даних для вивантаження немає." });

    public string Describe()
    {
        var parts = new List<string>();
        if (DirectoriesAdded > 0) parts.Add($"довідників — {DirectoriesAdded}");
        if (PeopleAdded > 0) parts.Add($"людей — {PeopleAdded}");
        if (StatusesUpdated > 0) parts.Add($"статусів — {StatusesUpdated}");

        var text = parts.Count == 0
            ? "Таблиця вже містить усе — нічого не додано."
            : "Записано в таблицю: " + string.Join(", ", parts) + ".";

        return Notes.Count == 0 ? text : text + Environment.NewLine + string.Join(Environment.NewLine, Notes);
    }
}

/// <summary>
/// Приймач даних: уміє дописувати у зовнішню таблицю те, що зʼявилося
/// в застосунку. Навмисно тільки дописує й ніколи не видаляє чужі рядки —
/// над тією самою таблицею працює ще й скрипт, і затирати його правки не можна.
/// </summary>
public interface IResponseSink
{
    string Title { get; }

    Task<PublishResult> PublishAsync(PublishPayload payload, CancellationToken ct = default);
}
