using Reports.Core.Abstractions;
using Reports.Core.Services;

namespace Reports.Integration.Tests;

public class SyncLimitsTests
{
    private static List<RawResponseRow> Rows(int count, string? settlement = null) =>
        Enumerable.Range(1, count).Select(i => new RawResponseRow
        {
            RowKey = $"k{i}",
            SubmittedAt = DateTime.Today,
            OperatorEmail = "op@example.com",
            SettlementName = settlement ?? "Київ",
            UavTypeName = "Мавік",
            TargetTypeName = "Шахед",
            UavCount = 1
        }).ToList();

    [Fact]
    public void Defaults_are_5000_rows_and_500_characters()
    {
        var limits = new SyncLimits();
        Assert.Equal(5000, limits.MaxRows);
        Assert.Equal(500, limits.MaxCellLength);
    }

    [Fact]
    public async Task Rows_over_the_limit_are_cut_and_the_summary_says_so()
    {
        var missions = new FakeMissions();
        var service = new ImportService(missions, new FakeReferences(), new SyncLimits(MaxRows: 5000));

        var result = await service.ImportAsync(new FakeSource(Rows(5600)));

        Assert.Equal(5000, result.TotalRows);
        Assert.Equal(5000, missions.Saved.Count);
        Assert.True(result.Truncated);
        Assert.Contains("5000", result.Describe());
        Assert.Contains("5600", result.Describe());
        Assert.Contains("обрізано", result.Describe());
    }

    [Fact]
    public async Task Exactly_at_the_limit_is_not_reported_as_truncated()
    {
        var service = new ImportService(new FakeMissions(), new FakeReferences(), new SyncLimits(MaxRows: 100));

        var result = await service.ImportAsync(new FakeSource(Rows(100)));

        Assert.False(result.Truncated);
        Assert.DoesNotContain("обрізано", result.Describe());
    }

    [Fact]
    public async Task Long_cells_are_cut_to_the_limit_and_reported()
    {
        var service = new ImportService(new FakeMissions(), new FakeReferences(), new SyncLimits(MaxCellLength: 500));

        var result = await service.ImportAsync(new FakeSource(Rows(3, settlement: new string('ж', 5000))));

        Assert.True(result.Truncated);
        Assert.All(result.UnknownValues, v => Assert.True(v.Length <= 500));
        Assert.Contains("500", result.Describe());
    }

    [Fact]
    public async Task Cancelled_sync_saves_nothing()
    {
        var missions = new FakeMissions();
        var service = new ImportService(missions, new FakeReferences());
        using var cts = new CancellationTokenSource();
        var source = new FakeSource(Rows(2000), onLoad: cts.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ImportAsync(source, cts.Token));

        Assert.Equal(0, missions.AddRangeCalls);
        Assert.Empty(missions.Saved);
    }

    [Fact]
    public async Task Already_cancelled_token_stops_before_any_save()
    {
        var missions = new FakeMissions();
        var service = new ImportService(missions, new FakeReferences());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new FakeSource(Rows(10)), cts.Token));

        Assert.Equal(0, missions.AddRangeCalls);
    }
}
