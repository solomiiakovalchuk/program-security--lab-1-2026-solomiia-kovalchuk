using System.Globalization;
using System.Text;
using System.Text.Json;
using Reports.Core.Audit;
using Reports.Core.Identity.Abstractions;

namespace Reports.Audit;

/// <summary>Export file format.</summary>
public enum AuditExportFormat
{
    /// <summary>Comma separated values, UTF-8 with BOM.</summary>
    Csv,

    /// <summary>JSON array of objects.</summary>
    Json
}

/// <summary>Administrator-only export of the audit log with spreadsheet-formula protection; the export itself is audited.</summary>
public sealed class AuditExportService
{
    private const string ExportAction = "ExportAuditLog";

    private static readonly string[] Columns =
    [
        "id", "occurredAtUtc", "actorLogin", "actionType", "targetType", "targetId",
        "outcome", "details", "osUser", "machineName", "prevHash", "hash",
    ];

    private readonly IAuditRepository _repository;
    private readonly ISessionContext _session;
    private readonly IAccessPolicy _policy;
    private readonly IAuditSink _sink;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public AuditExportService(
        IAuditRepository repository,
        ISessionContext session,
        IAccessPolicy policy,
        IAuditSink sink,
        IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Writes all events matching the query filter (Skip and Take are ignored) and returns their count.</summary>
    public async Task<int> ExportAsync(AuditQuery query, AuditExportFormat format, Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        AuditAccessGuard.Demand(_session, _policy, _sink, ExportAction);

        var events = new List<AuditEvent>();
        var skip = 0;
        while (true)
        {
            var page = await _repository.QueryAsync(query.WithPage(skip, AuditQuery.MaxTake), ct).ConfigureAwait(false);
            events.AddRange(page);
            if (page.Count < AuditQuery.MaxTake)
            {
                break;
            }

            skip += page.Count;
        }

        if (format == AuditExportFormat.Csv)
        {
            await WriteCsvAsync(events, output, ct).ConfigureAwait(false);
        }
        else
        {
            await WriteJsonAsync(events, output, ct).ConfigureAwait(false);
        }

        var isAll = query.FromUtc is null && string.IsNullOrEmpty(query.ActorLogin)
            && string.IsNullOrEmpty(query.ActionType) && query.Outcome is null;
        _sink.Record(
            AuditAction.AuditExported,
            AuditTarget.Audit,
            null,
            AuditOutcome.Success,
            new Dictionary<string, string>
            {
                ["format"] = format.ToString().ToLowerInvariant(),
                ["scope"] = isAll ? AuditRetentionService.ScopeAll : "partial",
                ["from"] = query.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                ["to"] = (query.ToUtc ?? _clock.UtcNow).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                ["count"] = events.Count.ToString(CultureInfo.InvariantCulture),
            });

        return events.Count;
    }

    /// <summary>Exports to a file, replacing it.</summary>
    public async Task<int> ExportToFileAsync(AuditQuery query, AuditExportFormat format, string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        return await ExportAsync(query, format, stream, ct).ConfigureAwait(false);
    }

    /// <summary>Prefixes cells that a spreadsheet would read as a formula with an apostrophe.</summary>
    public static string SafeCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    private static string[] Cells(AuditEvent e) =>
    [
        e.Id.ToString(CultureInfo.InvariantCulture),
        e.OccurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        e.ActorLogin,
        e.ActionType,
        e.TargetType.ToString(),
        e.TargetId ?? string.Empty,
        e.Outcome.ToString(),
        e.Details,
        e.OsUser,
        e.MachineName,
        e.PrevHash,
        e.Hash,
    ];

    private static async Task WriteCsvAsync(List<AuditEvent> events, Stream output, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(true), leaveOpen: true);
        await writer.WriteAsync(string.Join(',', Columns) + "\r\n").ConfigureAwait(false);
        foreach (var e in events)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteAsync(string.Join(',', Cells(e).Select(c => CsvQuote(SafeCell(c)))) + "\r\n").ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string CsvQuote(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private static async Task WriteJsonAsync(List<AuditEvent> events, Stream output, CancellationToken ct)
    {
        await using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        json.WriteStartArray();
        foreach (var e in events)
        {
            json.WriteStartObject();
            var cells = Cells(e);
            for (var i = 0; i < Columns.Length; i++)
            {
                json.WriteString(Columns[i], SafeCell(cells[i]));
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();
        await json.FlushAsync(ct).ConfigureAwait(false);
    }
}
