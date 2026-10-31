using System.Text;
using System.Text.Json;
using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;

namespace Reports.Audit.Tests;

public class AuditExportTests
{
    [Fact]
    public async Task Csv_HasHeaderRows_AndFormulaCellsArePrefixed()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Administrator, "=cmd");
        f.Add(AuditAction.ReportGenerated, "@evil");
        f.Add(AuditAction.ReportGenerated, "-1+1");

        using var ms = new MemoryStream();
        var count = await f.Export.ExportAsync(new AuditQuery(), AuditExportFormat.Csv, ms);
        var text = Encoding.UTF8.GetString(ms.ToArray()).TrimStart('﻿');

        Assert.Equal(2, count);
        Assert.StartsWith("id,occurredAtUtc,actorLogin", text);
        Assert.Contains("'=cmd", text);
        Assert.Contains("'@evil", text);
        Assert.Contains("'-1+1", text);
        Assert.Equal(3, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Json_IsValidAndFormulaCellsArePrefixed()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Administrator, "+admin");
        f.Add(AuditAction.ReportGenerated, "x");

        using var ms = new MemoryStream();
        await f.Export.ExportAsync(new AuditQuery(), AuditExportFormat.Json, ms);
        using var doc = JsonDocument.Parse(ms.ToArray());

        var row = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("'+admin", row.GetProperty("actorLogin").GetString());
        Assert.Equal(AuditAction.ReportGenerated, row.GetProperty("actionType").GetString());
    }

    [Fact]
    public async Task Export_IsItselfAudited()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Administrator);
        f.Add();

        await f.Export.ExportAsync(new AuditQuery(), AuditExportFormat.Csv, new MemoryStream());

        var last = (await f.Repository.QueryAsync(new AuditQuery())).First();
        Assert.Equal(AuditAction.AuditExported, last.ActionType);
        Assert.Contains("format=csv", last.Details);
        Assert.Contains("count=1", last.Details);
    }

    [Fact]
    public async Task Export_ByOperator_IsDenied_AndNothingWritten()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Operator, "op");
        using var ms = new MemoryStream();

        await Assert.ThrowsAsync<AccessDeniedException>(() => f.Export.ExportAsync(new AuditQuery(), AuditExportFormat.Csv, ms));
        Assert.Equal(0, ms.Length);
    }

    [Fact]
    public async Task Export_WithoutLogin_IsDenied()
    {
        using var f = new AuditFixture();
        await Assert.ThrowsAsync<AccessDeniedException>(() => f.Export.ExportAsync(new AuditQuery(), AuditExportFormat.Csv, new MemoryStream()));
    }

    [Fact]
    public void SafeCell_PrefixesOnlyDangerousStarts()
    {
        Assert.Equal("'=1", AuditExportService.SafeCell("=1"));
        Assert.Equal("'+1", AuditExportService.SafeCell("+1"));
        Assert.Equal("'-1", AuditExportService.SafeCell("-1"));
        Assert.Equal("'@a", AuditExportService.SafeCell("@a"));
        Assert.Equal("abc", AuditExportService.SafeCell("abc"));
        Assert.Equal(string.Empty, AuditExportService.SafeCell(null));
    }
}
