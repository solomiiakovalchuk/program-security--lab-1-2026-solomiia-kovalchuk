using System.Globalization;
using Reports.Core.Audit;
using Reports.Core.Identity;

namespace Reports.Core.Services;

/// <summary>Saves settings only for ChangeSettings and audits the NAMES of the changed fields (never values).</summary>
public sealed class SettingsService
{
    private readonly AppSettings _settings;
    private readonly Action _persist;
    private readonly AccessGuard _guard;
    private readonly IAuditSink _audit;
    private readonly Action<string, string>? _spreadsheetIdChanged;
    private Dictionary<string, string> _saved;

    /// <summary>Creates the service; <paramref name="persist"/> writes the settings, the hook gets (oldId, newId) when the spreadsheet id changes.</summary>
    public SettingsService(
        AppSettings settings,
        Action persist,
        AccessGuard guard,
        IAuditSink? audit = null,
        Action<string, string>? spreadsheetIdChanged = null)
    {
        _settings = settings;
        _persist = persist;
        _guard = guard;
        _audit = audit ?? NullAuditSink.Instance;
        _spreadsheetIdChanged = spreadsheetIdChanged;
        _saved = Snapshot(settings);
    }

    /// <summary>Persists the settings and records Settings.Changed with the changed field names.</summary>
    public void Save()
    {
        _guard.Demand(AccessActions.ChangeSettings);

        var current = Snapshot(_settings);
        var changed = current
            .Where(p => !_saved.TryGetValue(p.Key, out var old) || old != p.Value)
            .Select(p => p.Key)
            .ToList();

        try
        {
            if (changed.Contains(nameof(AppSettings.SpreadsheetId)))
            {
                _spreadsheetIdChanged?.Invoke(
                    _saved.GetValueOrDefault(nameof(AppSettings.SpreadsheetId), string.Empty),
                    _settings.SpreadsheetId);
            }

            _persist();
            _saved = Snapshot(_settings);
        }
        catch (Exception)
        {
            Record(AuditOutcome.Failure, changed);
            throw;
        }

        if (changed.Count > 0)
        {
            Record(AuditOutcome.Success, changed);
        }
    }

    private void Record(AuditOutcome outcome, List<string> changed) =>
        _audit.Record(
            AuditAction.SettingsChanged, AuditTarget.Settings, null, outcome,
            new Dictionary<string, string> { ["fields"] = string.Join(",", changed) });

    // ConfirmedSpreadsheetId is maintained by the confirmation flow, not by the user.
    private static Dictionary<string, string> Snapshot(AppSettings settings) =>
        typeof(AppSettings).GetProperties()
            .Where(p => p.Name != nameof(AppSettings.ConfirmedSpreadsheetId))
            .ToDictionary(
                p => p.Name,
                p => Convert.ToString(p.GetValue(settings), CultureInfo.InvariantCulture) ?? string.Empty);
}
