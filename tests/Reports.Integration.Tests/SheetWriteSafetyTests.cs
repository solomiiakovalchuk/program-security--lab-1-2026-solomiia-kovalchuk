using Google.Apis.Sheets.v4.Data;
using Reports.Core.Abstractions;
using Reports.Core.Domain;
using Reports.Core.Services;
using Reports.Integration;

namespace Reports.Integration.Tests;

public class SheetWriteSafetyTests
{
    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("plain", "plain")]
    [InlineData("a=b", "a=b")]
    [InlineData(" =x", " =x")]
    [InlineData("", "")]
    public void Neutralize_prefixes_only_dangerous_leading_characters(string input, string expected)
    {
        Assert.Equal(expected, SheetValueSanitizer.Neutralize(input));
    }

    [Fact]
    public void Neutralize_is_null_safe()
    {
        Assert.Equal(string.Empty, SheetValueSanitizer.Neutralize((string?)null));
        Assert.Equal(string.Empty, SheetValueSanitizer.NeutralizeCell(null));
    }

    private static GoogleSheetsWriter NewWriter(AppSettings settings) =>
        new(new SentinelFactory(), settings,
            new SpreadsheetConfirmation(settings, new FakeInfoProvider(), (_, _) => Task.FromResult(true)));

    private static IList<IList<object>> Rows(params object[][] rows) =>
        rows.Select(r => (IList<object>)r).ToList();

    private static IEnumerable<string> AllValues(BatchUpdateValuesRequest request) =>
        request.Data.SelectMany(d => d.Values).SelectMany(r => r).Select(v => v.ToString()!);

    [Fact]
    public void Request_is_RAW_and_all_planned_values_are_neutralised()
    {
        var settings = new AppSettings();
        var writer = NewWriter(settings);
        var updates = new List<ValueRange>();
        var notes = new List<string>();

        var payload = new PublishPayload
        {
            Uav = new[] { new DirectoryPair("=HYPERLINK(\"http://x\")", "+full") },
            Targets = new[] { new DirectoryPair("-target", "@full") },
            Munitions = new[] { new DirectoryPair("safe", "=1+1") },
            Settlements = new[] { new DirectoryPair("@place", "region") },
            People = new[]
            {
                new Person { Email = "=a@b.c", FullName = "+Name", Rank = "-Rank", Position = "@Pos" }
            },
            Statuses = new[] { new StatusChange("ts|2", "=СКАСОВАНО") }
        };

        var dirRows = Rows(new object[0], new object[0]);
        var peopleRows = Rows(new object[] { "Email", "Name", "Rank", "Position" });
        var responseRows = Rows(
            new object[] { "Позначка часу", "Статус" },
            new object[] { "ts", "" });

        writer.PlanDirectories(dirRows, payload, updates);
        writer.PlanPeople(peopleRows, payload, updates);
        writer.PlanStatuses(responseRows, payload, updates, notes);

        var request = GoogleSheetsWriter.BuildRequest(updates);

        Assert.Equal("RAW", request.ValueInputOption);
        var values = AllValues(request).ToList();
        Assert.NotEmpty(values);
        Assert.Contains("'=HYPERLINK(\"http://x\")", values);
        Assert.Contains("'+full", values);
        Assert.Contains("'-target", values);
        Assert.Contains("'@full", values);
        Assert.Contains("'=1+1", values);
        Assert.Contains("'@place", values);
        Assert.Contains("'=a@b.c", values);
        Assert.Contains("'+Name", values);
        Assert.Contains("'-Rank", values);
        Assert.Contains("'@Pos", values);
        Assert.Contains("'=СКАСОВАНО", values);
        Assert.DoesNotContain(values, v => v.Length > 0 && "=+-@".Contains(v[0]));
    }

    [Fact]
    public void Plain_values_are_not_changed_by_BuildRequest()
    {
        var updates = new List<ValueRange>
        {
            new() { Range = "A1:B1", Values = new List<IList<object>> { new object[] { "Мавік", "Мавік 3" } } }
        };

        var request = GoogleSheetsWriter.BuildRequest(updates);

        Assert.Equal(new[] { "Мавік", "Мавік 3" }, AllValues(request));
        Assert.Equal("A1:B1", request.Data.Single().Range);
    }
}
