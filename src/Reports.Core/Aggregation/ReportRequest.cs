using Reports.Core.Domain;
using Reports.Core.Periods;

namespace Reports.Core.Aggregation;

/// <summary>Формат вихідного документа.</summary>
public enum ReportFormat
{
    Word = 0,
    Pdf = 1
}

/// <summary>
/// Усе, що користувач задає у вікні формування рапорту.
/// Підписант обирається окремо від даних навмисно: рапорт зведений
/// по підрозділу, підписує його командир, а не кожен оператор.
/// </summary>
public sealed class ReportRequest
{
    public required DatePeriod Period { get; init; }

    /// <summary>Хто підписує рапорт. Обов'язково.</summary>
    public required Person Signer { get; init; }

    /// <summary>Необов'язковий фільтр даних за оператором (email).</summary>
    public string? OperatorEmail { get; init; }

    /// <summary>Необов'язковий фільтр за населеним пунктом.</summary>
    public int? SettlementId { get; init; }

    /// <summary>Бойовий наказ — редаговане поле з дефолтом із налаштувань.</summary>
    public string Order { get; init; } = string.Empty;

    /// <summary>Назва підрозділу, що виконував завдання.</summary>
    public string Subunit { get; init; } = string.Empty;

    public ReportFormat Format { get; init; } = ReportFormat.Word;

    /// <summary>Чи проходить запис усі фільтри цього запиту.</summary>
    public bool Matches(Mission mission)
    {
        if (!mission.CountsTowardsReport) return false;
        if (!Period.Contains(mission.Date)) return false;

        if (!string.IsNullOrWhiteSpace(OperatorEmail) &&
            !string.Equals(mission.OperatorEmail, OperatorEmail, StringComparison.OrdinalIgnoreCase))
            return false;

        if (SettlementId is { } id && mission.SettlementId != id)
            return false;

        return true;
    }
}
