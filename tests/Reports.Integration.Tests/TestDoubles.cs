using System.Security.Cryptography;
using Google.Apis.Sheets.v4;
using Reports.Core.Abstractions;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Integration;
using Reports.Security;

namespace Reports.Integration.Tests;

internal sealed class FakeMasterKeyProvider : IMasterKeyProvider
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public byte[] GetOrCreateMasterKey() => (byte[])_key.Clone();
}

internal sealed class TempFolder : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rpt-int-" + Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

internal sealed class RecordingAudit : IAuditSink
{
    public List<(string Action, AuditTarget Target, AuditOutcome Outcome, IReadOnlyDictionary<string, string>? Details)>
        Events { get; } = new();

    public void Record(
        string action, AuditTarget target, string? targetId, AuditOutcome outcome,
        IReadOnlyDictionary<string, string>? details = null) =>
        Events.Add((action, target, outcome, details));
}

internal sealed class FakeInfoProvider : ISpreadsheetInfoProvider
{
    public string Title { get; set; } = "Fake title";
    public int Calls { get; private set; }

    public Task<string> GetTitleAsync(string spreadsheetId, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Title);
    }
}

/// <summary>Service factory that must never create a real service; it records the requested access.</summary>
internal sealed class SentinelFactory : IGoogleServiceFactory
{
    public List<GoogleAccess> Requests { get; } = new();

    public Task<SheetsService> CreateServiceAsync(GoogleAccess access, CancellationToken ct = default)
    {
        Requests.Add(access);
        throw new SentinelException();
    }
}

internal sealed class SentinelException : Exception { }

internal sealed class FakeSource : IResponseSource
{
    private readonly IReadOnlyList<RawResponseRow> _rows;
    private readonly Action? _onLoad;

    public FakeSource(IReadOnlyList<RawResponseRow> rows, Action? onLoad = null)
    {
        _rows = rows;
        _onLoad = onLoad;
    }

    public string Title => "fake";

    public Task<RawDirectories> LoadDirectoriesAsync(CancellationToken ct = default) =>
        Task.FromResult(new RawDirectories());

    public Task<IReadOnlyList<RawResponseRow>> LoadResponsesAsync(CancellationToken ct = default)
    {
        _onLoad?.Invoke();
        return Task.FromResult(_rows);
    }
}

internal sealed class FakeMissions : IMissionRepository
{
    public List<Mission> Saved { get; } = new();
    public int AddRangeCalls { get; private set; }

    public Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Mission>>(Saved);

    public Task<Mission?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult<Mission?>(null);

    public Task<IReadOnlySet<string>> GetImportedKeysAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<string>>(Saved.Select(m => m.SourceRowKey).ToHashSet());

    public Task AddRangeAsync(IEnumerable<Mission> missions, CancellationToken ct = default)
    {
        AddRangeCalls++;
        Saved.AddRange(missions);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Mission mission, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeReferences : IReferenceRepository
{
    public List<UavType> Uav { get; } = new() { new UavType { Id = 1, ShortName = "Мавік" } };
    public List<TargetType> Targets { get; } = new() { new TargetType { Id = 2, ShortName = "Шахед" } };
    public List<MunitionType> Munitions { get; } = new();
    public List<Settlement> Settlements { get; } = new() { new Settlement { Id = 3, ShortName = "Київ" } };

    public Task<IReadOnlyList<UavType>> GetUavTypesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UavType>>(Uav);

    public Task<IReadOnlyList<TargetType>> GetTargetTypesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TargetType>>(Targets);

    public Task<IReadOnlyList<MunitionType>> GetMunitionTypesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MunitionType>>(Munitions);

    public Task<IReadOnlyList<Settlement>> GetSettlementsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Settlement>>(Settlements);

    public Task SaveAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem => Task.CompletedTask;

    public Task DeleteAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem => Task.CompletedTask;
}
