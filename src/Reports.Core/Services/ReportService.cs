using Reports.Core.Abstractions;
using Reports.Core.Aggregation;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>Помилка, зрозуміла користувачеві: показується у вікні, а не в стек-трейсі.</summary>
public sealed class ReportException : Exception
{
    public ReportException(string message) : base(message) { }
}

/// <summary>
/// Формування рапорту: відбір епізодів, зведення, створення файлу,
/// запис у журнал. Рендерери підставляються ззовні за форматом.
/// </summary>
public sealed class ReportService
{
    private readonly IMissionRepository _missions;
    private readonly IJournalRepository _journal;
    private readonly ReportAggregator _aggregator;
    private readonly IReadOnlyList<IReportRenderer> _renderers;
    private readonly AppSettings _settings;
    private readonly AccessGuard? _guard;
    private readonly IAuditSink _audit;

    public ReportService(
        IMissionRepository missions,
        IJournalRepository journal,
        ReportAggregator aggregator,
        IEnumerable<IReportRenderer> renderers,
        AppSettings settings,
        AccessGuard? guard = null,
        IAuditSink? audit = null)
    {
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
        _missions = missions;
        _journal = journal;
        _aggregator = aggregator;
        _renderers = renderers.ToList();
        _settings = settings;
    }

    /// <summary>Обчислити вміст рапорту без створення файлу — для попереднього перегляду.</summary>
    public async Task<ReportContent> PreviewAsync(ReportRequest request, CancellationToken ct = default)
    {
        _guard?.Demand(AccessActions.GenerateReport);
        Validate(request);
        var all = await _missions.GetAllAsync(ct);
        return _aggregator.Aggregate(request, all);
    }

    /// <summary>Сформувати документ і зареєструвати його у журналі.</summary>
    public async Task<string> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        _guard?.Demand(AccessActions.GenerateReport);

        try
        {
            var path = await GenerateCoreAsync(request, ct);
            return path;
        }
        catch (Exception ex) when (ex is not Identity.Exceptions.AccessDeniedException)
        {
            _audit.Record(AuditAction.ReportGenerated, AuditTarget.Report, null, AuditOutcome.Failure);
            throw;
        }
    }

    private async Task<string> GenerateCoreAsync(ReportRequest request, CancellationToken ct)
    {
        var content = await PreviewAsync(request, ct);

        if (content.IsEmpty)
            throw new ReportException(
                "За вказаний період записів немає. Перевірте межі періоду та фільтри " +
                "або синхронізуйте дані з джерела.");

        var renderer = _renderers.FirstOrDefault(r => r.Format == request.Format)
            ?? throw new ReportException($"Немає обробника для формату {request.Format}.");

        var folder = string.IsNullOrWhiteSpace(_settings.OutputFolder)
            ? Path.Combine(AppContext.BaseDirectory, "Рапорти")
            : _settings.OutputFolder;
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, BuildFileName(request));
        var saved = await renderer.RenderAsync(content, path, ct);

        await _journal.AddAsync(new JournalEntry
        {
            GeneratedAt = DateTime.Now,
            PeriodFrom = request.Period.From,
            PeriodTo = request.Period.To,
            SignerName = request.Signer.FullName,
            OperatorFilter = request.OperatorEmail ?? string.Empty,
            SettlementFilter = request.SettlementId?.ToString() ?? string.Empty,
            Order = request.Order,
            MissionCount = content.MissionCount,
            FilePath = saved
        }, ct);

        // Only the file name, period and count are logged, never the report content.
        _audit.Record(AuditAction.ReportGenerated, AuditTarget.Report, null, AuditOutcome.Success,
            new Dictionary<string, string>
            {
                ["file"] = Path.GetFileName(saved),
                ["period"] = $"{request.Period.From:yyyy-MM-dd}..{request.Period.To:yyyy-MM-dd}",
                ["count"] = content.MissionCount.ToString()
            });

        return saved;
    }

    private static void Validate(ReportRequest request)
    {
        if (!request.Signer.CanSign)
            throw new ReportException(
                $"Для підписанта «{request.Signer.Display}» не заповнені ПІБ, звання або посада. " +
                "Доповніть довідник «Люди».");

        if (request.Period.DayCount > 366)
            throw new ReportException("Період більший за рік — звузьте межі.");
    }

    private static string BuildFileName(ReportRequest request)
    {
        var name = $"Рапорт_{request.Signer.FullName}_" +
                   $"{request.Period.From:dd.MM.yyyy}_{request.Period.To:dd.MM.yyyy}";

        foreach (var bad in Path.GetInvalidFileNameChars())
            name = name.Replace(bad, '_');

        var extension = request.Format == ReportFormat.Pdf ? ".pdf" : ".docx";
        return name + extension;
    }
}
