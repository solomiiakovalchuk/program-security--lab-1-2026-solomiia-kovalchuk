using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Reports.Documents;

/// <summary>
/// Заміна тегів у тексті документа Word.
/// <para>
/// Складність у тому, що Word вільно ріже текст абзацу на кілька «прогонів» (Run):
/// тег <c>{{ПІБ}}</c> може лежати як <c>{{ПІ</c> + <c>Б}}</c>, і проста заміна
/// по кожному Run нічого не знайде. Тому текст абзацу склеюється, заміна
/// виконується цілком, а результат кладеться у перший прогін; решта очищується.
/// Форматування першого прогону при цьому зберігається.
/// </para>
/// </summary>
public static class TagReplacer
{
    public static void ReplaceInBody(Body body, IReadOnlyDictionary<string, string> values)
    {
        foreach (var paragraph in body.Descendants<Paragraph>())
            ReplaceInParagraph(paragraph, values);
    }

    public static void ReplaceInParagraph(Paragraph paragraph, IReadOnlyDictionary<string, string> values)
    {
        var runs = paragraph.Elements<Run>().ToList();
        if (runs.Count == 0) return;

        var texts = runs.SelectMany(r => r.Elements<Text>()).ToList();
        if (texts.Count == 0) return;

        var original = string.Concat(texts.Select(t => t.Text));
        if (!ContainsAnyTag(original, values)) return;

        var replaced = ReplaceOnePass(original, values);

        // Багаторядкове значення (адресат, резолюція) розбиваємо на рядки
        // з м'якими переносами всередині одного абзацу.
        var lines = replaced.Replace("\r\n", "\n").Split('\n');

        var first = texts[0];
        first.Text = lines[0];
        first.Space = SpaceProcessingModeValues.Preserve;

        for (int i = 1; i < texts.Count; i++)
        {
            texts[i].Text = string.Empty;
            texts[i].Space = SpaceProcessingModeValues.Preserve;
        }

        // Додаткові рядки чіпляємо після першого прогону, зберігаючи його стиль.
        var anchorRun = first.Parent as Run;
        if (anchorRun is null || lines.Length == 1) return;

        var insertAfter = (OpenXmlElement)anchorRun;
        for (int i = 1; i < lines.Length; i++)
        {
            var run = (Run)anchorRun.CloneNode(true);
            run.RemoveAllChildren<Text>();
            run.AppendChild(new Break());
            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });

            insertAfter.InsertAfterSelf(run);
            insertAfter = run;
        }
    }

    /// <summary>
    /// Заміна за один прохід: підставлене значення вже не скануємо на теги,
    /// тож дані з таблиці на кшталт <c>{{ПІБ}}</c> не стають тегами (SEC-08).
    /// </summary>
    internal static string ReplaceOnePass(string text, IReadOnlyDictionary<string, string> values)
    {
        var tags = values.Keys.Where(k => k.Length > 0).OrderByDescending(k => k.Length).ToList();
        if (tags.Count == 0) return text;

        var pattern = string.Join("|", tags.Select(System.Text.RegularExpressions.Regex.Escape));
        return System.Text.RegularExpressions.Regex.Replace(
            text, pattern, m => values[m.Value] ?? string.Empty);
    }

    private static bool ContainsAnyTag(string text, IReadOnlyDictionary<string, string> values)
    {
        foreach (var tag in values.Keys)
            if (text.Contains(tag, StringComparison.Ordinal))
                return true;
        return false;
    }
}
