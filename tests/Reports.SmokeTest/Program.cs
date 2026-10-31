using System.Text;
using Reports.Core.Abstractions;
using Reports.Core.Aggregation;
using Reports.Core.Periods;
using Reports.Core.Services;
using Reports.Data;
using Reports.Data.Repositories;
using Reports.Documents;
using Reports.Integration;

Console.OutputEncoding = Encoding.UTF8;

// Наскрізна перевірка ланцюга: база → імпорт демонстраційних даних →
// зведення за період → формування .docx → журнал.
// Працює в тимчасовій теці, тому нічого не псує в робочих даних.

var folder = Path.Combine(Path.GetTempPath(), "ReportsSmokeTest_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(folder);
Console.WriteLine($"Робоча тека: {folder}");

int failures = 0;

void Check(string name, bool condition, string? detail = null)
{
    Console.WriteLine(condition ? $"  [OK]    {name}" : $"  [ПОМИЛКА] {name}");
    if (!condition)
    {
        failures++;
        if (detail is not null) Console.WriteLine($"           {detail}");
    }
}

try
{
    var settings = new AppSettings
    {
        OutputFolder = Path.Combine(folder, "Рапорти"),
        SubunitText = "взводом перехоплювачів БпЛА зенітної батареї військової частини А0000",
        OrderDefault = "командира військової частини від 01.01.2026 №1т"
    };

    Console.WriteLine("\n1. Створення бази та довідників");
    var contextFactory = new SqliteContextFactory(Path.Combine(folder, "reports.db"));
    await DatabaseInitializer.EnsureCreatedAsync(contextFactory);

    var references = new ReferenceRepository(contextFactory);
    var people = new PersonRepository(contextFactory);
    var missions = new MissionRepository(contextFactory);
    var journal = new JournalRepository(contextFactory);

    var uavTypes = await references.GetUavTypesAsync();
    var allPeople = await people.GetAllAsync();
    Check("довідник БпЛА наповнено", uavTypes.Count > 0);
    Check("довідник людей наповнено", allPeople.Count > 0);

    Console.WriteLine("\n2. Імпорт демонстраційних відповідей");
    var import = new ImportService(missions, references);
    var result = await import.ImportAsync(new DemoResponseSource());
    Console.WriteLine("  " + result.Describe().Replace("\n", "\n  "));
    Check("рядки імпортовані", result.Imported > 0);
    Check("невідомих значень довідника немає", result.UnknownValues.Count == 0,
          string.Join(", ", result.UnknownValues));

    Console.WriteLine("\n3. Повторний імпорт не створює дублів");
    var again = await import.ImportAsync(new DemoResponseSource());
    Check("нових записів немає", again.Imported == 0, $"імпортовано {again.Imported}");
    Check("усі рядки розпізнані як вже наявні", again.Skipped == again.TotalRows);

    Console.WriteLine("\n4. Зведення за період");
    var signer = allPeople.First(p => p.CanSign);
    var request = new ReportRequest
    {
        Period = DatePeriod.Of(DateTime.Today.AddDays(-20), DateTime.Today),
        Signer = signer,
        Order = settings.OrderDefault,
        Subunit = settings.SubunitText
    };

    var content = ReportAggregator.CreateDefault().Aggregate(request, await missions.GetAllAsync());
    Check("епізоди відібрані", content.MissionCount > 0);
    Check("перелік БпЛА не порожній", content[ReportTags.UavWithCount].Length > 0);
    Check("перелік боєприпасів не порожній", content[ReportTags.Munitions].Length > 0);
    Check("перелік цілей не порожній", content[ReportTags.TargetTypes].Length > 0);
    Check("область підтягнута з довідника місць", content[ReportTags.Regions].Length > 0);
    Check("у тексті не лишилося незамінених тегів",
          !content.BuildSentence().Contains("{{"), content.BuildSentence());

    Console.WriteLine("\n  Текст рапорту:");
    Console.WriteLine("  " + content.BuildSentence());

    Console.WriteLine("\n5. Скасовані записи не потрапляють у рапорт");
    var all = await missions.GetAllAsync();
    var cancelled = all.Count(m => m.Status == Reports.Core.Domain.RecordStatus.Cancelled);
    Check("демонстраційні дані містять скасований запис", cancelled > 0);
    Check("скасовані не враховані у зведенні",
          content.MissionCount == all.Count(m => m.CountsTowardsReport && request.Period.Contains(m.Date)));

    Console.WriteLine("\n6. Формування документа");
    var renderer = new DocxReportRenderer(settings, Path.Combine(folder, "template.docx"));
    renderer.EnsureTemplateExists();
    Check("шаблон створено", File.Exists(renderer.TemplatePath));

    var service = new ReportService(missions, journal, ReportAggregator.CreateDefault(),
                                    new IReportRenderer[] { renderer }, settings);
    var path = await service.GenerateAsync(request);
    Check("файл рапорту створено", File.Exists(path));
    Check("файл не порожній", new FileInfo(path).Length > 1000);
    Console.WriteLine($"  Файл: {path}");

    Console.WriteLine("\n7. Журнал");
    var entries = await journal.GetAllAsync();
    Check("запис у журналі з'явився", entries.Count == 1);
    Check("у журналі збережено кількість епізодів", entries[0].MissionCount == content.MissionCount);

    Console.WriteLine("\n8. Порожній період дає зрозумілу помилку");
    var emptyRequest = new ReportRequest
    {
        Period = DatePeriod.Of(new DateTime(2000, 1, 1), new DateTime(2000, 1, 2)),
        Signer = signer,
        Order = settings.OrderDefault,
        Subunit = settings.SubunitText
    };
    try
    {
        await service.GenerateAsync(emptyRequest);
        Check("очікувалася помилка про відсутність записів", false);
    }
    catch (ReportException ex)
    {
        Check("повідомлення про відсутність записів", ex.Message.Contains("записів немає"), ex.Message);
    }

    Console.WriteLine("\n9. Складання діапазонів A1 для запису в таблицю");
    Check("індекс 0 дає колонку A", SheetRange.Column(0) == "A", SheetRange.Column(0));
    Check("індекс 9 дає колонку J", SheetRange.Column(9) == "J", SheetRange.Column(9));
    Check("індекс 25 дає колонку Z", SheetRange.Column(25) == "Z", SheetRange.Column(25));
    Check("індекс 26 дає колонку AA", SheetRange.Column(26) == "AA", SheetRange.Column(26));

    // Типова назва аркуша від Google — з пробілами; без лапок діапазон не розбереться.
    var quoted = SheetRange.Of("Відповіді форми 1", "A1:Z10");
    Check("назва аркуша з пробілами взята в лапки",
          quoted == "'Відповіді форми 1'!A1:Z10", quoted);

    var cellRange = SheetRange.Cell("Люди", 3, 7);
    Check("клітинка складається правильно", cellRange == "'Люди'!D7", cellRange);

    Console.WriteLine("\n10. Збір даних для вивантаження");
    var publish = new PublishService(references, people, missions);
    var payload = await publish.CollectAsync();
    var allMissions = await missions.GetAllAsync();

    Check("довідник БпЛА потрапив у вивантаження", payload.Uav.Count > 0);
    Check("люди потрапили у вивантаження", payload.People.Count > 0);
    Check("статуси зібрані для всіх подач", payload.Statuses.Count == allMissions.Count);
    Check("у блоці місць права колонка — область",
          payload.Settlements.Any(x => x.Right == "Харківська"),
          string.Join("; ", payload.Settlements.Select(x => x.Left + "/" + x.Right)));

    var cancelledStatus = payload.Statuses.FirstOrDefault(x => x.Marker == "СКАСОВАНО");
    Check("скасований запис має маркер СКАСОВАНО", cancelledStatus.SourceRowKey is not null);
    Check("ключ джерела містить номер рядка для адресації клітинки",
          cancelledStatus.SourceRowKey?.Contains('|') == true, cancelledStatus.SourceRowKey);

    Console.WriteLine("\n11. Пресети періодів");
    var monday = new DateTime(2026, 9, 2); // середа
    foreach (var preset in PeriodPreset.All)
    {
        var period = preset.For(monday);
        Check($"пресет «{preset.Title}» дає коректний проміжок", period.From <= period.To,
              period.ToString());
    }
}
catch (Exception ex)
{
    failures++;
    Console.WriteLine("\nНЕОБРОБЛЕНИЙ ВИНЯТОК:");
    Console.WriteLine(ex);
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "ПІДСУМОК: усі перевірки пройдено."
    : $"ПІДСУМОК: невдалих перевірок — {failures}.");

return failures == 0 ? 0 : 1;
