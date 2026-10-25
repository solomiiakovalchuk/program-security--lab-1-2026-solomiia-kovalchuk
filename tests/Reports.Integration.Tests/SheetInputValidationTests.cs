using DocumentFormat.OpenXml.Wordprocessing;
using Reports.Core.Abstractions;
using Reports.Core.Aggregation;
using Reports.Core.Domain;
using Reports.Core.Periods;
using Reports.Core.Services;
using Reports.Documents;

namespace Reports.Integration.Tests;

public class SheetInputValidationTests
{
    [Theory]
    [InlineData("operator@example.com", true)]
    [InlineData("a.b+c@sub.example.org", true)]
    [InlineData("no-at-sign", false)]
    [InlineData("a@b", false)]
    [InlineData("a b@c.d", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Email_format(string? value, bool expected) =>
        Assert.Equal(expected, SheetInputValidator.IsValidEmail(value));

    [Fact]
    public void Overlong_email_is_invalid() =>
        Assert.False(SheetInputValidator.IsValidEmail(new string('a', 250) + "@x.com"));

    [Theory]
    [InlineData("1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_", true)]
    [InlineData("short", false)]
    [InlineData("has space 1AbCdEfGhIjKlMnOpQrStUvWx", false)]
    [InlineData("https://docs.google.com/spreadsheets/d/xx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Spreadsheet_id_format(string? value, bool expected) =>
        Assert.Equal(expected, SheetInputValidator.IsValidSpreadsheetId(value));

    [Fact]
    public void Spreadsheet_id_length_bounds()
    {
        Assert.True(SheetInputValidator.IsValidSpreadsheetId(new string('x', 20)));
        Assert.False(SheetInputValidator.IsValidSpreadsheetId(new string('x', 19)));
        Assert.True(SheetInputValidator.IsValidSpreadsheetId(new string('x', 100)));
        Assert.False(SheetInputValidator.IsValidSpreadsheetId(new string('x', 101)));
    }

    [Fact]
    public async Task Source_and_writer_reject_a_bad_spreadsheet_id_before_any_call()
    {
        var factory = new SentinelFactory();
        var settings = new AppSettings { SpreadsheetId = "bad id!" , UploadEnabled = true };

        var source = new Reports.Integration.GoogleSheetsResponseSource(factory, settings);
        await Assert.ThrowsAsync<Reports.Integration.GoogleAccessException>(() => source.LoadResponsesAsync());

        var writer = new Reports.Integration.GoogleSheetsWriter(
            factory, settings,
            new Reports.Integration.SpreadsheetConfirmation(settings, new FakeInfoProvider(), (_, _) => Task.FromResult(true)));
        await Assert.ThrowsAsync<Reports.Integration.GoogleAccessException>(() => writer.PublishAsync(new PublishPayload()));

        Assert.Empty(factory.Requests);
    }

    [Fact]
    public void Clip_and_count_and_date_rules()
    {
        Assert.Equal(new string('x', 200), SheetInputValidator.Clip(new string('x', 300), 200, out var clipped));
        Assert.True(clipped);
        Assert.Equal(string.Empty, SheetInputValidator.Clip(null, 200, out clipped));
        Assert.False(clipped);

        Assert.Equal(999, SheetInputValidator.ClampUavCount(5000, out var exceeded));
        Assert.True(exceeded);
        Assert.Equal(0, SheetInputValidator.ClampUavCount(-4, out exceeded));
        Assert.False(exceeded);

        var today = new DateTime(2026, 6, 1);
        Assert.True(SheetInputValidator.IsPlausibleDate(new DateTime(2026, 5, 31), today));
        Assert.False(SheetInputValidator.IsPlausibleDate(new DateTime(1850, 1, 1), today));
        Assert.False(SheetInputValidator.IsPlausibleDate(new DateTime(2090, 1, 1), today));
    }

    private static RawResponseRow Row(
        string key = "k1", string email = "op@example.com", int count = 1,
        DateTime? date = null, DateTime? submitted = null, string settlement = "Київ") => new()
    {
        RowKey = key,
        SubmittedAt = submitted ?? DateTime.Today,
        Date = date,
        OperatorEmail = email,
        SettlementName = settlement,
        UavTypeName = "Мавік",
        TargetTypeName = "Шахед",
        UavCount = count
    };

    private static async Task<(ImportResult Result, FakeMissions Missions)> Import(params RawResponseRow[] rows)
    {
        var missions = new FakeMissions();
        var service = new ImportService(missions, new FakeReferences());
        var result = await service.ImportAsync(new FakeSource(rows));
        return (result, missions);
    }

    [Fact]
    public async Task Negative_count_becomes_zero_and_empty_date_becomes_submission_day_without_flag()
    {
        var submitted = DateTime.Today.AddDays(-2).AddHours(10);
        var (result, missions) = await Import(Row(count: -3, date: null, submitted: submitted));

        var mission = Assert.Single(missions.Saved);
        Assert.Equal(0, mission.Targets.Single().UavCount);
        Assert.Equal(submitted.Date, mission.Date);
        Assert.Equal(RecordStatus.Active, mission.Status);
        Assert.Equal(0, result.NeedsAttention);
    }

    [Fact]
    public async Task Count_above_range_is_clamped_and_flagged()
    {
        var (result, missions) = await Import(Row(count: 100000));

        var mission = Assert.Single(missions.Saved);
        Assert.Equal(999, mission.Targets.Single().UavCount);
        Assert.Equal(RecordStatus.NeedsAttention, mission.Status);
        Assert.Equal(1, result.NeedsAttention);
    }

    [Fact]
    public async Task Invalid_email_is_dropped_and_row_flagged()
    {
        var (_, missions) = await Import(Row(email: "not-an-email"));

        var mission = Assert.Single(missions.Saved);
        Assert.Equal(string.Empty, mission.OperatorEmail);
        Assert.Equal(RecordStatus.NeedsAttention, mission.Status);
    }

    [Fact]
    public async Task Impossible_date_falls_back_to_submission_day_and_is_flagged()
    {
        var submitted = DateTime.Today;
        var (_, missions) = await Import(Row(date: new DateTime(1850, 1, 1), submitted: submitted));

        var mission = Assert.Single(missions.Saved);
        Assert.Equal(submitted.Date, mission.Date);
        Assert.Equal(RecordStatus.NeedsAttention, mission.Status);
    }

    [Fact]
    public async Task Long_names_are_capped_at_200_characters()
    {
        var longName = new string('я', 400);
        var (result, missions) = await Import(Row(settlement: longName));

        Assert.Single(missions.Saved);
        Assert.All(result.UnknownValues, v => Assert.True(v.Length <= SheetInputValidator.MaxNameLength));
    }

    [Fact]
    public async Task Valid_row_is_imported_unchanged()
    {
        var (result, missions) = await Import(Row(count: 3));

        var mission = Assert.Single(missions.Saved);
        Assert.Equal(RecordStatus.Active, mission.Status);
        Assert.Equal("op@example.com", mission.OperatorEmail);
        Assert.Equal(3, mission.Targets.Single().UavCount);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task Period_longer_than_366_days_is_rejected_and_366_is_accepted()
    {
        var signer = new Person { Email = "s@x.com", FullName = "A B", Rank = "r", Position = "p" };
        var report = new ReportService(
            new FakeMissions(), null!, ReportAggregator.CreateDefault(), Array.Empty<IReportRenderer>(), new AppSettings());
        var from = new DateTime(2025, 1, 1);

        await Assert.ThrowsAsync<ReportException>(() => report.PreviewAsync(
            new ReportRequest { Period = DatePeriod.Of(from, from.AddDays(366)), Signer = signer }));

        var content = await report.PreviewAsync(
            new ReportRequest { Period = DatePeriod.Of(from, from.AddDays(365)), Signer = signer });
        Assert.NotNull(content);
    }

    private static Paragraph ParagraphOf(string text) =>
        new(new Run(new Text(text)));

    [Fact]
    public void Values_are_never_interpreted_as_tags_replacement_is_one_pass()
    {
        var values = new Dictionary<string, string>
        {
            ["{{ОПЕРАТОР}}"] = "{{ПІДПИСАНТ}}",
            ["{{ПІДПИСАНТ}}"] = "SECRET-SIGNER",
        };
        var paragraph = ParagraphOf("Оператор: {{ОПЕРАТОР}}; підписант: {{ПІДПИСАНТ}}");

        TagReplacer.ReplaceInParagraph(paragraph, values);

        Assert.Equal("Оператор: {{ПІДПИСАНТ}}; підписант: SECRET-SIGNER", paragraph.InnerText);
    }
}
