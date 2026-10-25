using Google.Apis.Sheets.v4;
using Reports.Core.Audit;
using Reports.Core.Services;

namespace Reports.Integration;

/// <summary>Reads the title of a spreadsheet so the administrator can confirm the write target.</summary>
public interface ISpreadsheetInfoProvider
{
    /// <summary>Returns the spreadsheet title.</summary>
    Task<string> GetTitleAsync(string spreadsheetId, CancellationToken ct = default);
}

/// <summary>Reads the title through the Sheets API with read-only access.</summary>
public sealed class GoogleSpreadsheetInfoProvider : ISpreadsheetInfoProvider
{
    private readonly IGoogleServiceFactory _factory;

    /// <summary>Creates a provider that obtains a read-only service from the factory.</summary>
    public GoogleSpreadsheetInfoProvider(IGoogleServiceFactory factory) => _factory = factory;

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(string spreadsheetId, CancellationToken ct = default)
    {
        using var service = await _factory.CreateServiceAsync(GoogleAccess.ReadOnly, ct);
        var request = service.Spreadsheets.Get(spreadsheetId);
        request.Fields = "properties.title";
        try
        {
            var meta = await request.ExecuteAsync(ct);
            return meta.Properties?.Title ?? string.Empty;
        }
        catch (Google.GoogleApiException ex)
        {
            throw new GoogleAccessException(
                "Не вдалося відкрити таблицю, щоб показати її назву: " + (ex.Error?.Message ?? ex.Message), ex);
        }
    }
}

/// <summary>Makes the administrator confirm the target spreadsheet before the first write and after an ID change (SEC-14).</summary>
public sealed class SpreadsheetConfirmation
{
    private readonly AppSettings _settings;
    private readonly ISpreadsheetInfoProvider _info;
    private readonly Func<string, string, Task<bool>> _confirm;
    private readonly IAuditSink _audit;
    private readonly Action? _saveSettings;

    /// <summary>Creates the check; confirm receives (title, id); saveSettings persists ConfirmedSpreadsheetId.</summary>
    public SpreadsheetConfirmation(
        AppSettings settings,
        ISpreadsheetInfoProvider info,
        Func<string, string, Task<bool>> confirm,
        IAuditSink? audit = null,
        Action? saveSettings = null)
    {
        _settings = settings;
        _info = info;
        _confirm = confirm;
        _audit = audit ?? NullAuditSink.Instance;
        _saveSettings = saveSettings;
    }

    /// <summary>Records an ID change and resets the confirmation; call when the ID is saved in Settings.</summary>
    public void OnSpreadsheetIdChanged(string oldId, string newId)
    {
        if (string.Equals(oldId, newId, StringComparison.Ordinal)) return;

        _audit.Record(
            AuditAction.GoogleSpreadsheetChanged, AuditTarget.GoogleSync, null, AuditOutcome.Success,
            new Dictionary<string, string> { ["fromId"] = oldId, ["toId"] = newId });
        _settings.ConfirmedSpreadsheetId = string.Empty;
        _saveSettings?.Invoke();
    }

    /// <summary>True when the current spreadsheet is confirmed (asking the administrator if needed).</summary>
    public async Task<bool> EnsureConfirmedAsync(CancellationToken ct = default)
    {
        var id = _settings.SpreadsheetId;
        if (string.Equals(_settings.ConfirmedSpreadsheetId, id, StringComparison.Ordinal)) return true;

        // The ID was edited without going through OnSpreadsheetIdChanged: still audit and reset.
        if (_settings.ConfirmedSpreadsheetId.Length > 0)
            OnSpreadsheetIdChanged(_settings.ConfirmedSpreadsheetId, id);

        var title = await _info.GetTitleAsync(id, ct);
        if (!await _confirm(title, id)) return false;

        _settings.ConfirmedSpreadsheetId = id;
        _saveSettings?.Invoke();
        return true;
    }
}
