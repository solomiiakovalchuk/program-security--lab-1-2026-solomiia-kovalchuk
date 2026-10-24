using Reports.Core.Domain;

namespace Reports.Core.Aggregation;

/// <summary>Правило обчислення одного тега шаблону.</summary>
public interface IAggregationRule
{
    string Tag { get; }
    string Evaluate(AggregationContext context);
}

/// <summary>
/// Вхідні дані для правил: відібрані епізоди періоду та параметри запиту.
/// </summary>
public sealed class AggregationContext
{
    public required ReportRequest Request { get; init; }
    public required IReadOnlyList<Mission> Missions { get; init; }

    public IEnumerable<TargetEngagement> Engagements =>
        Missions.SelectMany(m => m.Targets);

    public bool IsEmpty => Missions.Count == 0;
}

/// <summary>
/// Спільна основа правил: тримає тег і дає нащадкам дрібні утиліти
/// (склеювання переліків, відсів порожніх значень).
/// </summary>
public abstract class AggregationRule : IAggregationRule
{
    public abstract string Tag { get; }

    public abstract string Evaluate(AggregationContext context);

    /// <summary>Склеювання переліку через кому з відсівом порожніх елементів.</summary>
    protected static string JoinList(IEnumerable<string> parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>Унікальні повні назви довідникових елементів у порядку першої появи.</summary>
    protected static IEnumerable<string> DistinctNames<T>(IEnumerable<T?> items)
        where T : ReferenceItem =>
        items.Where(i => i is not null)
             .Select(i => i!.ForDocument())
             .Where(n => !string.IsNullOrWhiteSpace(n))
             .Distinct(StringComparer.CurrentCultureIgnoreCase);
}
