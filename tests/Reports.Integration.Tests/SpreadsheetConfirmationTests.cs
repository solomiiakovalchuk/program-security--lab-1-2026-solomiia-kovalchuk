using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Services;
using Reports.Integration;

namespace Reports.Integration.Tests;

public class SpreadsheetConfirmationTests
{
    private static readonly string IdA = new('a', 44);
    private static readonly string IdB = new('b', 44);

    private sealed class Harness
    {
        public AppSettings Settings { get; }
        public FakeInfoProvider Info { get; } = new();
        public RecordingAudit Audit { get; } = new();
        public List<(string Title, string Id)> Asked { get; } = new();
        public bool Answer { get; set; } = true;
        public int Saves { get; private set; }
        public SpreadsheetConfirmation Confirmation { get; }

        public Harness(string id)
        {
            Settings = new AppSettings { SpreadsheetId = id };
            Confirmation = new SpreadsheetConfirmation(
                Settings, Info,
                (title, sid) => { Asked.Add((title, sid)); return Task.FromResult(Answer); },
                Audit, () => Saves++);
        }
    }

    [Fact]
    public async Task First_write_asks_with_title_and_id_then_confirmed_once()
    {
        var h = new Harness(IdA);
        h.Info.Title = "Рапорти БпЛА";

        Assert.True(await h.Confirmation.EnsureConfirmedAsync());
        Assert.True(await h.Confirmation.EnsureConfirmedAsync());

        Assert.Single(h.Asked);
        Assert.Equal(("Рапорти БпЛА", IdA), h.Asked[0]);
        Assert.Equal(IdA, h.Settings.ConfirmedSpreadsheetId);
        Assert.Equal(1, h.Info.Calls);
        Assert.Equal(1, h.Saves);
    }

    [Fact]
    public async Task Changed_id_asks_again_and_is_audited_with_ids_only()
    {
        var h = new Harness(IdA);
        await h.Confirmation.EnsureConfirmedAsync();

        h.Settings.SpreadsheetId = IdB;
        Assert.True(await h.Confirmation.EnsureConfirmedAsync());

        Assert.Equal(2, h.Asked.Count);
        Assert.Equal(IdB, h.Asked[1].Id);
        Assert.Equal(IdB, h.Settings.ConfirmedSpreadsheetId);

        var changed = Assert.Single(h.Audit.Events);
        Assert.Equal(AuditAction.GoogleSpreadsheetChanged, changed.Action);
        Assert.Equal(AuditTarget.GoogleSync, changed.Target);
        Assert.Equal(new[] { "fromId", "toId" }, changed.Details!.Keys.OrderBy(k => k));
        Assert.Equal(IdA, changed.Details["fromId"]);
        Assert.Equal(IdB, changed.Details["toId"]);
        Assert.DoesNotContain(h.Info.Title, changed.Details.Values);
    }

    [Fact]
    public void Explicit_id_change_resets_confirmation_and_emits_audit()
    {
        var h = new Harness(IdB);
        h.Settings.ConfirmedSpreadsheetId = IdA;

        h.Confirmation.OnSpreadsheetIdChanged(IdA, IdB);

        Assert.Equal(string.Empty, h.Settings.ConfirmedSpreadsheetId);
        Assert.Single(h.Audit.Events);
    }

    [Fact]
    public async Task Declined_confirmation_writes_nothing_and_requests_no_service()
    {
        var h = new Harness(IdA) { Answer = false };
        h.Settings.UploadEnabled = true;
        var factory = new SentinelFactory();
        var writer = new GoogleSheetsWriter(factory, h.Settings, h.Confirmation);

        var result = await writer.PublishAsync(new PublishPayload());

        Assert.Equal(0, result.DirectoriesAdded + result.PeopleAdded + result.StatusesUpdated);
        Assert.Empty(factory.Requests);
        Assert.Equal(string.Empty, h.Settings.ConfirmedSpreadsheetId);
        Assert.Single(h.Asked);
    }

    [Fact]
    public async Task Declined_confirmation_is_asked_again_next_time()
    {
        var h = new Harness(IdA) { Answer = false };

        Assert.False(await h.Confirmation.EnsureConfirmedAsync());
        Assert.False(await h.Confirmation.EnsureConfirmedAsync());

        Assert.Equal(2, h.Asked.Count);
    }
}
