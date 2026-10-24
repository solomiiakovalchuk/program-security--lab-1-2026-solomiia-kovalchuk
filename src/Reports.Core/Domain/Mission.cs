namespace Reports.Core.Domain;

/// <summary>
/// Одна подача форми = один виїзд (або доба роботи оператора).
/// </summary>
public sealed class Mission : Entity
{
    /// <summary>
    /// Стабільний ключ рядка-джерела (Timestamp + номер рядка аркуша «Відповіді»).
    /// Захищає від повторного імпорту тієї самої подачі.
    /// </summary>
    public string SourceRowKey { get; set; } = string.Empty;

    /// <summary>
    /// Дата виїзду. У формі питання необов'язкове: порожнє поле означає
    /// день подання, тому сюди вже потрапляє обчислене значення.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>Момент подання форми (колонка Timestamp).</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Email оператора, зібраний Google автоматично (перевірений збір).</summary>
    public string OperatorEmail { get; set; } = string.Empty;

    public int? SettlementId { get; set; }
    public Settlement? Settlement { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;

    public List<TargetEngagement> Targets { get; set; } = new();

    public override string Display => $"{Date:dd.MM.yyyy} — {Settlement?.ShortName ?? "—"}";

    /// <summary>Сумарна кількість БпЛА, витрачених за цю подачу.</summary>
    public int TotalUavCount => Targets.Sum(t => t.UavCount);

    /// <summary>Чи враховувати запис під час формування рапорту.</summary>
    public bool CountsTowardsReport => Status == RecordStatus.Active;
}

/// <summary>Ураження однієї цілі: тип БпЛА, тип цілі, кількість і застосовані боєприпаси.</summary>
public sealed class TargetEngagement : Entity
{
    public int MissionId { get; set; }
    public Mission? Mission { get; set; }

    public int? UavTypeId { get; set; }
    public UavType? UavType { get; set; }

    public int? TargetTypeId { get; set; }
    public TargetType? TargetType { get; set; }

    /// <summary>Кількість застосованих БпЛА цього типу.</summary>
    public int UavCount { get; set; }

    public List<MunitionUse> Munitions { get; set; } = new();

    public override string Display =>
        $"{UavType?.ShortName ?? "?"} × {UavCount} → {TargetType?.ShortName ?? "?"}";
}

/// <summary>Застосування одного боєприпасу в межах ураження цілі (до 3 на ціль).</summary>
public sealed class MunitionUse : Entity
{
    public int TargetEngagementId { get; set; }
    public TargetEngagement? TargetEngagement { get; set; }

    public int? MunitionTypeId { get; set; }
    public MunitionType? MunitionType { get; set; }

    public override string Display => MunitionType?.ShortName ?? "?";
}
