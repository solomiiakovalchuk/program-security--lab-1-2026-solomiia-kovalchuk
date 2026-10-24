namespace Reports.Core.Periods;

/// <summary>
/// Пресет періоду «одним кліком». Кожен нащадок сам вирішує, який
/// проміжок повернути відносно поточної дати — класичний поліморфізм,
/// який прибирає розгалуження switch з інтерфейсу.
/// </summary>
public abstract class PeriodPreset
{
    public abstract string Title { get; }

    protected abstract DatePeriod Resolve(DateTime today);

    public DatePeriod For(DateTime today) => Resolve(today.Date);

    public DatePeriod Now() => For(DateTime.Today);

    public override string ToString() => Title;

    /// <summary>Порядок пресетів у панелі вікна формування.</summary>
    public static IReadOnlyList<PeriodPreset> All { get; } = new PeriodPreset[]
    {
        new TodayPreset(),
        new YesterdayPreset(),
        new LastSevenDaysPreset(),
        new PreviousWeekPreset(),
        new CurrentMonthPreset(),
        new PreviousMonthPreset()
    };
}

public sealed class TodayPreset : PeriodPreset
{
    public override string Title => "Сьогодні";
    protected override DatePeriod Resolve(DateTime today) => new(today, today);
}

public sealed class YesterdayPreset : PeriodPreset
{
    public override string Title => "Вчора";
    protected override DatePeriod Resolve(DateTime today)
    {
        var d = today.AddDays(-1);
        return new DatePeriod(d, d);
    }
}

public sealed class LastSevenDaysPreset : PeriodPreset
{
    public override string Title => "7 днів";
    protected override DatePeriod Resolve(DateTime today) => new(today.AddDays(-6), today);
}

/// <summary>Минулий тиждень, понеділок–неділя.</summary>
public sealed class PreviousWeekPreset : PeriodPreset
{
    public override string Title => "Минулий тиждень";
    protected override DatePeriod Resolve(DateTime today)
    {
        int shift = ((int)today.DayOfWeek + 6) % 7;   // понеділок = 0
        var thisMonday = today.AddDays(-shift);
        var lastMonday = thisMonday.AddDays(-7);
        return new DatePeriod(lastMonday, lastMonday.AddDays(6));
    }
}

public sealed class CurrentMonthPreset : PeriodPreset
{
    public override string Title => "Цей місяць";
    protected override DatePeriod Resolve(DateTime today) =>
        new(new DateTime(today.Year, today.Month, 1), today);
}

public sealed class PreviousMonthPreset : PeriodPreset
{
    public override string Title => "Минулий місяць";
    protected override DatePeriod Resolve(DateTime today)
    {
        var firstOfThis = new DateTime(today.Year, today.Month, 1);
        var firstOfPrev = firstOfThis.AddMonths(-1);
        return new DatePeriod(firstOfPrev, firstOfThis.AddDays(-1));
    }
}
