using System.Globalization;
using Reports.Core.Domain;

namespace Reports.Core.Aggregation;

/// <summary>Дата початку періоду у форматі ДД.ММ.РРРР.</summary>
public sealed class DateFromRule : AggregationRule
{
    public override string Tag => ReportTags.DateFrom;
    public override string Evaluate(AggregationContext c) =>
        c.Request.Period.From.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

/// <summary>Дата кінця періоду у форматі ДД.ММ.РРРР.</summary>
public sealed class DateToRule : AggregationRule
{
    public override string Tag => ReportTags.DateTo;
    public override string Evaluate(AggregationContext c) =>
        c.Request.Period.To.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// Типи БпЛА із сумарною кількістю, сортування за спаданням кількості:
/// «FPV-дрон — 12 шт., дрон «Кажан» — 3 шт.»
/// </summary>
public sealed class UavSummaryRule : AggregationRule
{
    public override string Tag => ReportTags.UavWithCount;

    public override string Evaluate(AggregationContext c)
    {
        var groups = c.Engagements
            .Where(e => e.UavType is not null)
            .GroupBy(e => e.UavType!.ForDocument(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new { Name = g.Key, Count = g.Sum(e => e.UavCount) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name, StringComparer.CurrentCulture);

        return JoinList(groups.Select(g => $"{g.Name} — {g.Count} шт."));
    }
}

/// <summary>Унікальні повні назви боєприпасів через кому, без підрахунку одиниць.</summary>
public sealed class MunitionListRule : AggregationRule
{
    public override string Tag => ReportTags.Munitions;

    public override string Evaluate(AggregationContext c) =>
        JoinList(DistinctNames(c.Engagements
            .SelectMany(e => e.Munitions)
            .Select(m => m.MunitionType)));
}

/// <summary>Унікальні повні назви типів цілей (родовий відмінок) через кому.</summary>
public sealed class TargetTypeListRule : AggregationRule
{
    public override string Tag => ReportTags.TargetTypes;

    public override string Evaluate(AggregationContext c) =>
        JoinList(DistinctNames(c.Engagements.Select(e => e.TargetType)));
}

/// <summary>Усі населені пункти періоду через кому.</summary>
public sealed class SettlementListRule : AggregationRule
{
    public override string Tag => ReportTags.Settlements;

    public override string Evaluate(AggregationContext c) =>
        JoinList(DistinctNames(c.Missions.Select(m => m.Settlement)));
}

/// <summary>Області, підтягнуті з довідника місць.</summary>
public sealed class RegionListRule : AggregationRule
{
    public override string Tag => ReportTags.Regions;

    public override string Evaluate(AggregationContext c) =>
        JoinList(c.Missions
            .Select(m => m.Settlement?.Region)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase));
}

/// <summary>Назва підрозділу — стала з налаштувань.</summary>
public sealed class SubunitRule : AggregationRule
{
    public override string Tag => ReportTags.Subunit;
    public override string Evaluate(AggregationContext c) => c.Request.Subunit;
}

/// <summary>Бойовий наказ — з поля вікна формування.</summary>
public sealed class OrderRule : AggregationRule
{
    public override string Tag => ReportTags.Order;
    public override string Evaluate(AggregationContext c) => c.Request.Order;
}

/// <summary>Посада підписанта.</summary>
public sealed class SignerPositionRule : AggregationRule
{
    public override string Tag => ReportTags.Position;
    public override string Evaluate(AggregationContext c) => c.Request.Signer.Position;
}

/// <summary>Звання підписанта.</summary>
public sealed class SignerRankRule : AggregationRule
{
    public override string Tag => ReportTags.Rank;
    public override string Evaluate(AggregationContext c) => c.Request.Signer.Rank;
}

/// <summary>ПІБ підписанта.</summary>
public sealed class SignerNameRule : AggregationRule
{
    public override string Tag => ReportTags.SignerName;
    public override string Evaluate(AggregationContext c) => c.Request.Signer.FullName;
}
