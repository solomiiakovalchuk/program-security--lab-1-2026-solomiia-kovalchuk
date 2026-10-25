namespace Reports.Integration;

/// <summary>
/// Складання діапазонів у нотації A1.
/// <para>
/// Назву аркуша обовʼязково беремо в апострофи: типова назва від Google —
/// «Відповіді форми 1» із пробілами, і без лапок такий діапазон не розбирається.
/// </para>
/// </summary>
public static class SheetRange
{
    public static string Of(string sheet, string a1)
    {
        var name = (sheet ?? string.Empty).Trim().Replace("'", "''");
        return $"'{name}'!{a1}";
    }

    /// <summary>Літерне позначення колонки за індексом: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string Column(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

        var letters = string.Empty;
        for (int n = index; ; n = n / 26 - 1)
        {
            letters = (char)('A' + n % 26) + letters;
            if (n < 26) break;
        }

        return letters;
    }

    /// <summary>Одна клітинка: індекс колонки (з нуля) і номер рядка (з одиниці).</summary>
    public static string Cell(string sheet, int columnIndex, int rowNumber) =>
        Of(sheet, $"{Column(columnIndex)}{rowNumber}");
}
