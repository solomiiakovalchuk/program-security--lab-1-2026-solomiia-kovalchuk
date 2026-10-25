using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Reports.Core.Abstractions;
using Reports.Core.Aggregation;
using Reports.Core.Services;

namespace Reports.Documents;

/// <summary>
/// Формує рапорт у форматі Word: копіює шаблон, підставляє значення тегів,
/// зберігає результат. Word на машині не потрібен — файл збирається бібліотекою.
/// </summary>
public sealed class DocxReportRenderer : IReportRenderer
{
    private readonly AppSettings _settings;
    private readonly string _templatePath;

    public DocxReportRenderer(AppSettings settings, string templatePath)
    {
        _settings = settings;
        _templatePath = templatePath;
    }

    public ReportFormat Format => ReportFormat.Word;

    /// <summary>Шлях до шаблону, за яким формуються документи.</summary>
    public string TemplatePath => _templatePath;

    public Task<string> RenderAsync(ReportContent content, string outputPath, CancellationToken ct = default)
    {
        EnsureTemplateExists();

        var folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        outputPath = MakeUniquePath(outputPath);
        File.Copy(_templatePath, outputPath, overwrite: false);

        ct.ThrowIfCancellationRequested();

        using (var doc = WordprocessingDocument.Open(outputPath, isEditable: true))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new ReportException("Шаблон пошкоджений: у документі немає тіла.");

            TagReplacer.ReplaceInBody(body, content.Values);

            foreach (var header in doc.MainDocumentPart.HeaderParts)
                foreach (var paragraph in header.Header.Descendants<Paragraph>())
                    TagReplacer.ReplaceInParagraph(paragraph, content.Values);

            foreach (var footer in doc.MainDocumentPart.FooterParts)
                foreach (var paragraph in footer.Footer.Descendants<Paragraph>())
                    TagReplacer.ReplaceInParagraph(paragraph, content.Values);

            doc.MainDocumentPart.Document.Save();
        }

        return Task.FromResult(outputPath);
    }

    /// <summary>Створює шаблон за замовчуванням, якщо його ще немає.</summary>
    public void EnsureTemplateExists()
    {
        if (File.Exists(_templatePath)) return;
        new ReportTemplateBuilder(_settings).Build(_templatePath);
    }

    /// <summary>Перебудовує шаблон за поточними налаштуваннями, затираючи наявний.</summary>
    public void RebuildTemplate()
    {
        if (File.Exists(_templatePath)) File.Delete(_templatePath);
        new ReportTemplateBuilder(_settings).Build(_templatePath);
    }

    /// <summary>Два рапорти за той самий період не мають затирати один одного.</summary>
    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (int i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(folder, $"{name} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(folder, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    }
}
