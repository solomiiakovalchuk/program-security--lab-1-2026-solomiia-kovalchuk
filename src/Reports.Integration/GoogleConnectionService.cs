using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Services;

namespace Reports.Integration;

/// <summary>Checks the connection to the spreadsheet (a settings action) and records Google.ConnectionChecked.</summary>
public sealed class GoogleConnectionService
{
    private readonly IGoogleServiceFactory _factory;
    private readonly AppSettings _settings;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;

    /// <summary>Creates the service.</summary>
    public GoogleConnectionService(
        IGoogleServiceFactory factory, AppSettings settings, AccessGuard guard, IAuditSink? audit = null)
    {
        _factory = factory;
        _settings = settings;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
    }

    /// <summary>Diagnoses the spreadsheet; needs ChangeSettings.</summary>
    public async Task<GoogleSheetsResponseSource.Diagnosis> CheckAsync(CancellationToken ct = default)
    {
        _guard.Demand(AccessActions.ChangeSettings);

        try
        {
            var diagnosis = await new GoogleSheetsResponseSource(_factory, _settings).DiagnoseAsync(ct);
            Record(AuditOutcome.Success, diagnosis.IsUsable ? "usable" : "incomplete");
            return diagnosis;
        }
        catch (Exception)
        {
            Record(AuditOutcome.Failure, "error");
            throw;
        }
    }

    private void Record(AuditOutcome outcome, string result) =>
        _audit.Record(AuditAction.GoogleConnectionChecked, AuditTarget.GoogleSync, null, outcome,
            new Dictionary<string, string> { ["result"] = result });
}
