namespace Reports.Integration;

/// <summary>
/// Зіставлення колонок аркуша «Відповіді» з полями моделі за текстом заголовка.
/// Прив'язка за змістом, а не за номером колонки: питання у формі можна
/// переставити чи додати нове — імпорт продовжить працювати.
/// </summary>
public sealed class ResponseColumnMap
{
    public int Timestamp { get; private set; } = -1;
    public int Email { get; private set; } = -1;
    public int Date { get; private set; } = -1;
    public int Settlement { get; private set; } = -1;
    public int UavType { get; private set; } = -1;
    public int TargetType { get; private set; } = -1;
    public int UavCount { get; private set; } = -1;
    public int Status { get; private set; } = -1;

    private readonly List<int> _munitions = new();

    /// <summary>Колонки боєприпасів (до трьох на ціль), у порядку появи.</summary>
    public IReadOnlyList<int> Munitions => _munitions;

    public static ResponseColumnMap FromHeader(IList<object> header)
    {
        var map = new ResponseColumnMap();

        for (int i = 0; i < header.Count; i++)
        {
            var title = (header[i]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            if (title.Length == 0) continue;

            if (map.Timestamp < 0 && (title.Contains("позначка часу") || title.Contains("timestamp")))
            { map.Timestamp = i; continue; }

            if (map.Email < 0 && (title.Contains("адрес") || title.Contains("пошт") || title.Contains("email")))
            { map.Email = i; continue; }

            if (map.Settlement < 0 && (title.Contains("населен") || title.Contains("пункт")))
            { map.Settlement = i; continue; }

            if (map.UavCount < 0 && title.Contains("кількість"))
            { map.UavCount = i; continue; }

            if (map.UavType < 0 && (title.Contains("бпла") || title.Contains("дрон")))
            { map.UavType = i; continue; }

            if (map.TargetType < 0 && title.Contains("ціл"))
            { map.TargetType = i; continue; }

            if (title.Contains("боєприпас"))
            {
                // «Додати боєприпас 2?» — це питання розгалуження, а не сам боєприпас.
                if (!title.Contains("додати") && !title.Contains("?"))
                    map._munitions.Add(i);
                continue;
            }

            if (map.Status < 0 && title.Contains("статус"))
            { map.Status = i; continue; }

            if (map.Date < 0 && title.Contains("дата"))
                map.Date = i;
        }

        // Перша колонка форми — завжди позначка часу, навіть якщо заголовок незвичний.
        if (map.Timestamp < 0 && header.Count > 0) map.Timestamp = 0;

        return map;
    }

    /// <summary>Значення клітинки за індексом колонки; порожньо, якщо колонки немає.</summary>
    public string Value(IList<object> row, int column) =>
        column >= 0 && column < row.Count ? row[column]?.ToString()?.Trim() ?? string.Empty : string.Empty;

    /// <summary>Перелік полів, які не вдалося знайти — для діагностики у вікні синхронізації.</summary>
    public IReadOnlyList<string> MissingFields()
    {
        var missing = new List<string>();
        if (UavType < 0) missing.Add("тип БпЛА");
        if (TargetType < 0) missing.Add("тип цілі");
        if (UavCount < 0) missing.Add("кількість БпЛА");
        if (Settlement < 0) missing.Add("населений пункт");
        if (_munitions.Count == 0) missing.Add("боєприпас");
        return missing;
    }
}
