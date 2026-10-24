namespace Reports.Core.Aggregation;

/// <summary>
/// Обчислені значення тегів + зведена статистика. Це те, що піде
/// у шаблон документа і у вікно попереднього перегляду.
/// </summary>
public sealed class ReportContent
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public ReportContent(IReadOnlyDictionary<string, string> values, int missionCount, int engagementCount)
    {
        _values = values;
        MissionCount = missionCount;
        EngagementCount = engagementCount;
    }

    /// <summary>Скільки подач потрапило у рапорт.</summary>
    public int MissionCount { get; }

    /// <summary>Скільки епізодів ураження цілей потрапило у рапорт.</summary>
    public int EngagementCount { get; }

    public bool IsEmpty => MissionCount == 0;

    public IReadOnlyDictionary<string, string> Values => _values;

    public string this[string tag] => _values.TryGetValue(tag, out var v) ? v : string.Empty;

    /// <summary>Підстановка обчислених значень у довільний текст із тегами.</summary>
    public string Fill(string template)
    {
        var result = template;
        foreach (var (tag, value) in _values)
            result = result.Replace(tag, value, StringComparison.Ordinal);
        return result;
    }

    /// <summary>
    /// Текст рапорту одним реченням — саме він показується у прев'ю
    /// і підставляється у шаблон замість тіла документа.
    /// </summary>
    public string BuildSentence() => Fill(SentenceTemplate);

    /// <summary>
    /// Шаблон основного речення рапорту. Свідомо тримається тут, а не у
    /// .docx: прев'ю має бути доступним ще до створення файлу.
    /// </summary>
    public const string SentenceTemplate =
        "Дійсним доповідаю, що з " + ReportTags.DateFrom + " по " + ReportTags.DateTo +
        " було використано (витрачено) в ході бойових дій " + ReportTags.UavWithCount +
        ", з боєприпасами: " + ReportTags.Munitions +
        ", переобладнаними для виконання спеціальних завдань зі знищення повітряних цілей " +
        ReportTags.Subunit + " по знищенню " + ReportTags.TargetTypes +
        " в районі н.п. " + ReportTags.Settlements + ", " + ReportTags.Regions +
        " області, згідно бойових розпоряджень, бойового наказу " + ReportTags.Order + ".";
}
