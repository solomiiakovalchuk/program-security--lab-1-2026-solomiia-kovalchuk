namespace Reports.Core.Domain;

/// <summary>
/// Запис журналу сформованих рапортів: хто, коли, за який період і в якому форматі.
/// </summary>
public sealed class JournalEntry : Entity
{
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }

    public string SignerName { get; set; } = string.Empty;
    public string OperatorFilter { get; set; } = string.Empty;
    public string SettlementFilter { get; set; } = string.Empty;
    public string Order { get; set; } = string.Empty;

    /// <summary>Кількість епізодів, що потрапили у рапорт.</summary>
    public int MissionCount { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public override string Display =>
        $"{GeneratedAt:dd.MM.yyyy HH:mm} — {PeriodFrom:dd.MM} … {PeriodTo:dd.MM} ({SignerName})";
}
