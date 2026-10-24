using Reports.Core.Aggregation;
using Reports.Core.Domain;

namespace Reports.Core.Abstractions;

/// <summary>Формує файл документа за обчисленим вмістом рапорту.</summary>
public interface IReportRenderer
{
    ReportFormat Format { get; }

    /// <summary>Створює файл і повертає повний шлях до нього.</summary>
    Task<string> RenderAsync(ReportContent content, string outputPath, CancellationToken ct = default);
}

/// <summary>Сирий рядок відповіді з зовнішнього джерела (аркуш «Відповіді»).</summary>
public sealed class RawResponseRow
{
    /// <summary>Стабільний ключ рядка — Timestamp + номер рядка.</summary>
    public required string RowKey { get; init; }

    public DateTime SubmittedAt { get; init; }

    /// <summary>Дата з форми; порожня означає день подання.</summary>
    public DateTime? Date { get; init; }

    public string OperatorEmail { get; init; } = string.Empty;
    public string SettlementName { get; init; } = string.Empty;
    public string UavTypeName { get; init; } = string.Empty;
    public string TargetTypeName { get; init; } = string.Empty;
    public int UavCount { get; init; }

    /// <summary>Назви боєприпасів (до трьох на ціль).</summary>
    public IReadOnlyList<string> MunitionNames { get; init; } = Array.Empty<string>();

    public string StatusMarker { get; init; } = string.Empty;
}

/// <summary>Довідники, прочитані із зовнішнього джерела.</summary>
public sealed class RawDirectories
{
    public IReadOnlyList<(string Short, string Full)> Uav { get; init; } = Array.Empty<(string, string)>();
    public IReadOnlyList<(string Short, string Full)> Targets { get; init; } = Array.Empty<(string, string)>();
    public IReadOnlyList<(string Short, string Full)> Munitions { get; init; } = Array.Empty<(string, string)>();
    public IReadOnlyList<(string Short, string Region)> Settlements { get; init; } = Array.Empty<(string, string)>();
    public IReadOnlyList<Person> People { get; init; } = Array.Empty<Person>();
}

/// <summary>
/// Джерело даних форми. Реалізації: Google Sheets і демонстраційне
/// джерело з вигаданими даними для роботи без інтернету.
/// </summary>
public interface IResponseSource
{
    string Title { get; }

    Task<RawDirectories> LoadDirectoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RawResponseRow>> LoadResponsesAsync(CancellationToken ct = default);
}
