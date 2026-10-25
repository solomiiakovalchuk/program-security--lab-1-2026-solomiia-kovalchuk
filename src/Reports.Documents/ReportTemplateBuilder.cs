using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Reports.Core.Aggregation;
using Reports.Core.Services;

namespace Reports.Documents;

/// <summary>
/// Створює шаблон рапорту .docx із тегами.
/// <para>
/// Постійні частини (гриф, адресат, резолюція, назва підрозділу) вписуються
/// у шаблон при його створенні з <see cref="AppSettings"/>. Далі шаблон можна
/// вільно переверстувати руками — програма про оформлення нічого не знає,
/// вона лише замінює теги.
/// </para>
/// </summary>
public sealed class ReportTemplateBuilder
{
    // Word рахує відступи у твіпах: 1 пункт = 20 твіпів.
    private const int MarginLeft = 85 * 20;    // 30 мм
    private const int MarginRight = 28 * 20;   // 10 мм
    private const int MarginTop = 57 * 20;     // 20 мм
    private const int MarginBottom = 57 * 20;  // 20 мм
    private const int FirstLineIndent = 35 * 20; // ≈1,25 см
    private const string LineSpacing = "360";  // 240 = одинарний, 360 = 1,5

    private readonly AppSettings _settings;

    public ReportTemplateBuilder(AppSettings settings) => _settings = settings;

    /// <summary>Створює файл шаблону за вказаним шляхом, перезаписуючи наявний.</summary>
    public void Build(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        AppendStyles(main);

        body.AppendChild(Line(_settings.Classification, JustificationValues.Right, bold: true));
        body.AppendChild(Empty());

        body.AppendChild(Multiline(_settings.Addressee, JustificationValues.Right));
        body.AppendChild(Empty());

        body.AppendChild(Multiline(_settings.Resolution, JustificationValues.Left));
        body.AppendChild(Line($"{_settings.ResolutionRank}\t\t\t{_settings.ResolutionName}",
                              JustificationValues.Left));
        body.AppendChild(Empty());
        body.AppendChild(Empty());

        body.AppendChild(Line("РАПОРТ", JustificationValues.Center, bold: true));
        body.AppendChild(Empty());

        // Основне речення рапорту — саме воно наповнюється тегами.
        body.AppendChild(Line(ReportContent.SentenceTemplate,
                              JustificationValues.Both,
                              firstLineIndent: true));
        body.AppendChild(Empty());
        body.AppendChild(Empty());

        body.AppendChild(Line(ReportTags.Position, JustificationValues.Left));
        body.AppendChild(Line($"{ReportTags.Rank}\t\t\t{ReportTags.SignerName}",
                              JustificationValues.Left));

        body.AppendChild(SectionProperties());
        main.Document.Save();
    }

    private void AppendStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(
                    new RunPropertiesBaseStyle(
                        new RunFonts
                        {
                            Ascii = _settings.FontName,
                            HighAnsi = _settings.FontName,
                            ComplexScript = _settings.FontName
                        },
                        new FontSize { Val = (_settings.FontSize * 2).ToString() })),
                new ParagraphPropertiesDefault(
                    new ParagraphPropertiesBaseStyle(
                        new SpacingBetweenLines
                        {
                            After = "0",
                            Line = LineSpacing,
                            LineRule = LineSpacingRuleValues.Auto
                        }))));
        part.Styles.Save();
    }

    private Paragraph Empty() => Line(string.Empty, JustificationValues.Left);

    /// <summary>Абзац із кількох рядків, розділених \n у налаштуваннях.</summary>
    private Paragraph Multiline(string text, JustificationValues alignment)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var paragraph = Line(lines[0], alignment);

        var run = paragraph.Elements<Run>().First();
        for (int i = 1; i < lines.Length; i++)
        {
            run.AppendChild(new Break());
            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        return paragraph;
    }

    private Paragraph Line(
        string text,
        JustificationValues alignment,
        bool bold = false,
        bool firstLineIndent = false)
    {
        var runProperties = new RunProperties(
            new RunFonts
            {
                Ascii = _settings.FontName,
                HighAnsi = _settings.FontName,
                ComplexScript = _settings.FontName
            },
            new FontSize { Val = (_settings.FontSize * 2).ToString() });

        if (bold) runProperties.AppendChild(new Bold());

        var paragraphProperties = new ParagraphProperties(
            new Justification { Val = alignment },
            new SpacingBetweenLines
            {
                After = "0",
                Line = LineSpacing,
                LineRule = LineSpacingRuleValues.Auto
            });

        if (firstLineIndent)
            paragraphProperties.AppendChild(new Indentation { FirstLine = FirstLineIndent.ToString() });

        return new Paragraph(
            paragraphProperties,
            new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static SectionProperties SectionProperties() => new(
        new PageSize { Width = 11906, Height = 16838 },   // A4
        new PageMargin
        {
            Left = (uint)MarginLeft,
            Right = (uint)MarginRight,
            Top = MarginTop,
            Bottom = MarginBottom,
            Header = 0U,
            Footer = 0U,
            Gutter = 0U
        });
}
