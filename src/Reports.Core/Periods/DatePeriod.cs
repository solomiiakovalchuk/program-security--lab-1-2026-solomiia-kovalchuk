namespace Reports.Core.Periods;

/// <summary>
/// Період звітування — напівінтервал за календарними днями, обидві межі включно.
/// </summary>
public readonly record struct DatePeriod(DateTime From, DateTime To)
{
    public static DatePeriod Of(DateTime from, DateTime to)
    {
        var a = from.Date;
        var b = to.Date;
        return a <= b ? new DatePeriod(a, b) : new DatePeriod(b, a);
    }

    public bool Contains(DateTime date) => date.Date >= From && date.Date <= To;

    public int DayCount => (To - From).Days + 1;

    public override string ToString() => $"{From:dd.MM.yyyy} — {To:dd.MM.yyyy}";
}
