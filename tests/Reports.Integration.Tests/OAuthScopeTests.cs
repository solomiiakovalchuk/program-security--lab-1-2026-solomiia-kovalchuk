using Google.Apis.Sheets.v4;
using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Services;
using Reports.Integration;

namespace Reports.Integration.Tests;

public class OAuthScopeTests
{
    [Fact]
    public void ReadOnly_requests_only_the_readonly_scope()
    {
        Assert.Equal(new[] { SheetsService.Scope.SpreadsheetsReadonly }, GoogleScopes.For(GoogleAccess.ReadOnly));
    }

    [Fact]
    public void ReadWrite_requests_only_the_full_spreadsheets_scope()
    {
        Assert.Equal(new[] { SheetsService.Scope.Spreadsheets }, GoogleScopes.For(GoogleAccess.ReadWrite));
    }

    [Fact]
    public void Token_stores_are_separate_per_access()
    {
        Assert.Equal("token-ro", GoogleScopes.TokenFolderName(GoogleAccess.ReadOnly));
        Assert.Equal("token-rw", GoogleScopes.TokenFolderName(GoogleAccess.ReadWrite));
    }

    [Fact]
    public async Task Response_source_uses_read_only_access()
    {
        var factory = new SentinelFactory();
        var settings = new AppSettings { SpreadsheetId = new string('a', 44) };
        var source = new GoogleSheetsResponseSource(factory, settings);

        await Assert.ThrowsAsync<SentinelException>(() => source.LoadResponsesAsync());

        Assert.Equal(new[] { GoogleAccess.ReadOnly }, factory.Requests);
    }

    [Fact]
    public async Task Writer_refuses_when_upload_is_disabled_and_requests_no_service()
    {
        var factory = new SentinelFactory();
        var settings = new AppSettings { SpreadsheetId = new string('a', 44) };
        Assert.False(settings.UploadEnabled);
        var writer = NewWriter(factory, settings);

        var ex = await Assert.ThrowsAsync<GoogleAccessException>(() => writer.PublishAsync(new PublishPayload()));

        Assert.Contains("вимкнене", ex.Message);
        Assert.Empty(factory.Requests);
    }

    [Fact]
    public async Task Writer_requests_read_write_only_when_upload_is_enabled_and_confirmed()
    {
        var factory = new SentinelFactory();
        var settings = new AppSettings { SpreadsheetId = new string('a', 44), UploadEnabled = true };
        var writer = NewWriter(factory, settings);

        await Assert.ThrowsAsync<SentinelException>(() => writer.PublishAsync(new PublishPayload()));

        Assert.Equal(new[] { GoogleAccess.ReadWrite }, factory.Requests);
    }

    private static GoogleSheetsWriter NewWriter(IGoogleServiceFactory factory, AppSettings settings) =>
        new(factory, settings,
            new SpreadsheetConfirmation(settings, new FakeInfoProvider(), (_, _) => Task.FromResult(true)));
}
