using Reports.Core.Abstractions;
using Reports.Core.Aggregation;
using Reports.Core.Audit;
using Reports.Core.Domain;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;
using Reports.Core.Periods;
using Reports.Core.Services;

namespace Reports.Identity.Tests;

/// <summary>SEC-04: the role is checked in the application layer, not by hiding UI. Fakes only, no WPF.</summary>
public sealed class AccessGuardTests
{
    private static readonly string[] OperatorActionNames =
    {
        AccessActions.GenerateReport, AccessActions.ViewJournal, AccessActions.ViewResponses, AccessActions.Sync,
        AccessActions.CancelRecord, AccessActions.RestoreRecord, AccessActions.ChangeOwnPassword
    };

    private static readonly string[] AdminOnlyActionNames =
    {
        AccessActions.EditReferenceData, AccessActions.EditPeople, AccessActions.PublishToGoogle,
        AccessActions.ChangeSettings, AccessActions.RebuildTemplate, AccessActions.ForgetToken,
        AccessActions.ManageAccounts, AccessActions.ViewAuditLog, AccessActions.ExportAuditLog
    };

    public static IEnumerable<object[]> OperatorActions() => OperatorActionNames.Select(a => new object[] { a });

    public static IEnumerable<object[]> AdminOnlyActions() => AdminOnlyActionNames.Select(a => new object[] { a });

    private sealed class Rig
    {
        public Rig(Role? role)
        {
            Session = new TestSessionContext();
            if (role is not null)
            {
                Session.Current = new Session(Guid.NewGuid(), role.ToString()!.ToLowerInvariant(), role.Value);
            }

            Audit = new CapturingAuditSink();
            Guard = new AccessGuard(Session, new AccessPolicyService(), Audit);
        }

        public TestSessionContext Session { get; }
        public CapturingAuditSink Audit { get; }
        public AccessGuard Guard { get; }

        public int Denials => Audit.Events.Count(e => e.Action == AuditAction.AccessDenied);
    }

    // ---- the whole matrix ----

    [Theory]
    [MemberData(nameof(AdminOnlyActions))]
    public void AdminOnlyAction_IsDeniedForOperator_AndRecorded(string action)
    {
        var rig = new Rig(Role.Operator);

        Assert.False(rig.Guard.IsAllowed(action));
        Assert.Throws<AccessDeniedException>(() => rig.Guard.Demand(action));

        var denied = Assert.Single(rig.Audit.Events);
        Assert.Equal(AuditAction.AccessDenied, denied.Action);
        Assert.Equal(AuditOutcome.Denied, denied.Outcome);
        Assert.Equal(action, denied.Details["action"]);
        Assert.Equal("Operator", denied.Details["role"]);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyActions))]
    public void AdminOnlyAction_IsDeniedWithoutSession_AndRecordedWithoutRole(string action)
    {
        var rig = new Rig(null);

        Assert.False(rig.Guard.IsAllowed(action));
        Assert.Throws<AccessDeniedException>(() => rig.Guard.Demand(action));

        var denied = Assert.Single(rig.Audit.Events);
        Assert.Equal(AuditAction.AccessDenied, denied.Action);
        Assert.Equal(action, denied.Details["action"]);
        Assert.False(denied.Details.ContainsKey("role"));
    }

    [Theory]
    [MemberData(nameof(AdminOnlyActions))]
    public void AdminOnlyAction_IsAllowedForAdministrator(string action)
    {
        var rig = new Rig(Role.Administrator);

        Assert.True(rig.Guard.IsAllowed(action));
        Assert.Equal(Role.Administrator, rig.Guard.Demand(action).Role);
        Assert.Empty(rig.Audit.Events);
    }

    [Theory]
    [MemberData(nameof(OperatorActions))]
    public void OperatorAction_IsAllowedForBothRoles_ButNotWithoutSession(string action)
    {
        Assert.True(new Rig(Role.Operator).Guard.IsAllowed(action));
        Assert.True(new Rig(Role.Administrator).Guard.IsAllowed(action));

        var anonymous = new Rig(null);
        Assert.Throws<AccessDeniedException>(() => anonymous.Guard.Demand(action));
        Assert.Equal(1, anonymous.Denials);
    }

    [Fact]
    public void UnknownAction_IsDenied()
    {
        var rig = new Rig(Role.Administrator);
        Assert.Throws<AccessDeniedException>(() => rig.Guard.Demand("DoAnything"));
    }

    [Fact]
    public void IsAllowed_NeverWritesToTheAudit()
    {
        var rig = new Rig(Role.Operator);
        rig.Guard.IsAllowed(AccessActions.ChangeSettings);
        Assert.Empty(rig.Audit.Events);
    }

    // ---- guarded services really throw ----

    [Fact]
    public async Task EditReferenceData_Operator_Throws_AndNothingIsSaved()
    {
        var rig = new Rig(Role.Operator);
        var inner = new FakeReferences();
        var guarded = new GuardedReferenceRepository(inner, rig.Guard, rig.Audit);

        await Assert.ThrowsAsync<AccessDeniedException>(() => guarded.SaveAsync(new UavType { ShortName = "X" }));
        await Assert.ThrowsAsync<AccessDeniedException>(() => guarded.DeleteAsync(new UavType { Id = 3, ShortName = "X" }));

        Assert.Equal(0, inner.Writes);
        Assert.Equal(2, rig.Denials);
        Assert.Empty(await guarded.GetUavTypesAsync());
    }

    [Fact]
    public async Task EditReferenceData_Administrator_Saves_AndAuditsKindAndName()
    {
        var rig = new Rig(Role.Administrator);
        var inner = new FakeReferences();
        var guarded = new GuardedReferenceRepository(inner, rig.Guard, rig.Audit);

        await guarded.SaveAsync(new UavType { ShortName = "FPV" });
        await guarded.SaveAsync(new UavType { Id = 5, ShortName = "FPV" });
        await guarded.DeleteAsync(new UavType { Id = 5, ShortName = "FPV" });

        Assert.Equal(new[] { AuditAction.ReferenceAdded, AuditAction.ReferenceUpdated, AuditAction.ReferenceDeleted },
            rig.Audit.Events.Select(e => e.Action));
        Assert.All(rig.Audit.Events, e => Assert.Equal("FPV", e.Details["name"]));
        Assert.All(rig.Audit.Events, e => Assert.Equal(AuditOutcome.Success, e.Outcome));
    }

    [Fact]
    public async Task EditPeople_Operator_Throws_Administrator_AuditsEmail()
    {
        var op = new Rig(Role.Operator);
        var inner = new FakePeople();
        var guardedForOperator = new GuardedPersonRepository(inner, op.Guard, op.Audit);
        await Assert.ThrowsAsync<AccessDeniedException>(() => guardedForOperator.SaveAsync(new Person { Email = "a@b.c" }));
        await Assert.ThrowsAsync<AccessDeniedException>(() => guardedForOperator.DeleteAsync(new Person { Id = 1 }));
        Assert.Equal(0, inner.Writes);

        var admin = new Rig(Role.Administrator);
        var guardedForAdmin = new GuardedPersonRepository(inner, admin.Guard, admin.Audit);
        await guardedForAdmin.SaveAsync(new Person { Email = "a@b.c" });
        var saved = Assert.Single(admin.Audit.Events);
        Assert.Equal(AuditAction.PersonAdded, saved.Action);
        Assert.Equal("a@b.c", saved.Details["email"]);
    }

    [Fact]
    public async Task FailedSave_IsRecordedAsFailure()
    {
        var rig = new Rig(Role.Administrator);
        var guarded = new GuardedReferenceRepository(new FakeReferences { Fail = true }, rig.Guard, rig.Audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.SaveAsync(new UavType { ShortName = "X" }));

        Assert.Equal(AuditOutcome.Failure, Assert.Single(rig.Audit.Events).Outcome);
    }

    [Fact]
    public async Task PublishToGoogle_Operator_Throws_BeforeTheSinkIsTouched()
    {
        var rig = new Rig(Role.Operator);
        var sink = new FakeSink();
        var publish = new PublishService(new FakeReferences(), new FakePeople(), new FakeMissions(), rig.Guard, rig.Audit);

        await Assert.ThrowsAsync<AccessDeniedException>(() => publish.PublishAsync(sink));

        Assert.Equal(0, sink.Calls);
        Assert.Equal(1, rig.Denials);
    }

    [Fact]
    public async Task PublishToGoogle_Administrator_AuditsCompletion()
    {
        var rig = new Rig(Role.Administrator);
        var inner = new FakePeople();
        inner.Items.Add(new Person { Email = "x@y.z" });
        var sink = new FakeSink();
        var publish = new PublishService(new FakeReferences(), inner, new FakeMissions(), rig.Guard, rig.Audit);

        await publish.PublishAsync(sink);

        Assert.Equal(1, sink.Calls);
        var done = Assert.Single(rig.Audit.Events);
        Assert.Equal(AuditAction.GooglePublishCompleted, done.Action);
        Assert.Equal(AuditOutcome.Success, done.Outcome);
    }

    [Fact]
    public void ChangeSettings_Operator_Throws_AndNothingIsPersisted()
    {
        var rig = new Rig(Role.Operator);
        var persisted = 0;
        var settings = new AppSettings();
        var service = new SettingsService(settings, () => persisted++, rig.Guard, rig.Audit);
        settings.FontSize = 20;

        Assert.Throws<AccessDeniedException>(service.Save);

        Assert.Equal(0, persisted);
        Assert.Equal(1, rig.Denials);
    }

    [Fact]
    public void ChangeSettings_Administrator_AuditsOnlyFieldNames()
    {
        var rig = new Rig(Role.Administrator);
        var settings = new AppSettings();
        var service = new SettingsService(settings, () => { }, rig.Guard, rig.Audit);
        settings.FontSize = 20;
        settings.OutputFolder = @"C:\secret-looking\path";

        service.Save();
        service.Save(); // nothing changed: no second event

        var changed = Assert.Single(rig.Audit.Events);
        Assert.Equal(AuditAction.SettingsChanged, changed.Action);
        Assert.Equal("OutputFolder,FontSize", changed.Details["fields"]);
        Assert.DoesNotContain("secret-looking", changed.Details["fields"]);
    }

    [Fact]
    public void ChangeSettings_SpreadsheetIdChange_NotifiesTheConfirmationHook()
    {
        var rig = new Rig(Role.Administrator);
        var settings = new AppSettings { SpreadsheetId = "old" };
        (string, string)? seen = null;
        var service = new SettingsService(settings, () => { }, rig.Guard, rig.Audit, (o, n) => seen = (o, n));
        settings.SpreadsheetId = "new";

        service.Save();

        Assert.Equal(("old", "new"), seen);
    }

    [Fact]
    public void RebuildTemplate_Operator_Throws_AndTheActionDoesNotRun()
    {
        var rig = new Rig(Role.Operator);
        var runs = 0;
        var action = GuardedAction.RebuildTemplate(() => runs++, rig.Guard, rig.Audit);

        Assert.Throws<AccessDeniedException>(action.Execute);

        Assert.Equal(0, runs);
        Assert.Equal(1, rig.Denials);
    }

    [Fact]
    public void RebuildTemplate_Administrator_RunsAndAudits()
    {
        var rig = new Rig(Role.Administrator);
        var runs = 0;
        GuardedAction.RebuildTemplate(() => runs++, rig.Guard, rig.Audit).Execute();

        Assert.Equal(1, runs);
        Assert.Equal(AuditAction.TemplateRebuilt, Assert.Single(rig.Audit.Events).Action);
    }

    [Fact]
    public void ForgetToken_Operator_Throws_Administrator_Audits_AndFailureIsRecorded()
    {
        var op = new Rig(Role.Operator);
        var runs = 0;
        Assert.Throws<AccessDeniedException>(GuardedAction.ForgetToken(() => runs++, op.Guard, op.Audit).Execute);
        Assert.Equal(0, runs);

        var admin = new Rig(Role.Administrator);
        GuardedAction.ForgetToken(() => runs++, admin.Guard, admin.Audit).Execute();
        Assert.Equal(1, runs);
        Assert.Equal(AuditAction.GoogleTokenForgotten, Assert.Single(admin.Audit.Events).Action);

        var failing = new Rig(Role.Administrator);
        Assert.Throws<IOException>(
            GuardedAction.ForgetToken(() => throw new IOException(), failing.Guard, failing.Audit).Execute);
        Assert.Equal(AuditOutcome.Failure, Assert.Single(failing.Audit.Events).Outcome);
    }

    [Fact]
    public async Task CancelAndRestore_NeedASession_AndAreAuditedForOperator()
    {
        var missions = new FakeMissions();
        missions.Items.Add(new Mission { Id = 1, Status = RecordStatus.Active });

        var anonymous = new Rig(null);
        var denied = new MissionStatusService(missions, anonymous.Guard, anonymous.Audit);
        await Assert.ThrowsAsync<AccessDeniedException>(() => denied.CancelAsync(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => denied.RestoreAsync(1));
        await Assert.ThrowsAsync<AccessDeniedException>(() => denied.ToggleAsync(1));
        Assert.Equal(RecordStatus.Active, missions.Items[0].Status);
        Assert.Equal(3, anonymous.Denials);

        var op = new Rig(Role.Operator);
        var service = new MissionStatusService(missions, op.Guard, op.Audit);
        await service.CancelAsync(1);
        Assert.Equal(RecordStatus.Cancelled, missions.Items[0].Status);
        await service.ToggleAsync(1);
        Assert.Equal(RecordStatus.Active, missions.Items[0].Status);

        Assert.Equal(new[] { AuditAction.MissionCancelled, AuditAction.MissionRestored }, op.Audit.Events.Select(e => e.Action));
        Assert.Null(await service.CancelAsync(99));
    }

    [Fact]
    public async Task ViewResponsesAndJournal_AreGuardedOnRead()
    {
        var anonymous = new Rig(null);
        var missions = new GuardedMissionRepository(new FakeMissions(), anonymous.Guard);
        var journal = new GuardedJournalRepository(new FakeJournal(), anonymous.Guard);

        await Assert.ThrowsAsync<AccessDeniedException>(() => missions.GetAllAsync());
        await Assert.ThrowsAsync<AccessDeniedException>(() => journal.GetAllAsync());

        var op = new Rig(Role.Operator);
        Assert.Empty(await new GuardedMissionRepository(new FakeMissions(), op.Guard).GetAllAsync());
        Assert.Empty(await new GuardedJournalRepository(new FakeJournal(), op.Guard).GetAllAsync());
    }

    [Fact]
    public async Task GenerateReport_WithoutSession_Throws()
    {
        var rig = new Rig(null);
        var service = new ReportService(
            new FakeMissions(), new FakeJournal(), ReportAggregator.CreateDefault(),
            Array.Empty<IReportRenderer>(), new AppSettings(), rig.Guard, rig.Audit);
        var request = new ReportRequest { Period = DatePeriod.Of(DateTime.Today, DateTime.Today), Signer = new Person() };

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GenerateAsync(request));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.PreviewAsync(request));
    }

    [Fact]
    public async Task Sync_WithoutSession_Throws_OperatorImportsAndIsAudited()
    {
        var anonymous = new Rig(null);
        var denied = new ImportService(new FakeMissions(), new FakeReferences(), null, anonymous.Guard, anonymous.Audit);
        await Assert.ThrowsAsync<AccessDeniedException>(() => denied.ImportAsync(new FakeSource()));

        var op = new Rig(Role.Operator);
        var import = new ImportService(new FakeMissions(), new FakeReferences(), null, op.Guard, op.Audit);
        var result = await import.ImportAsync(new FakeSource());
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(AuditAction.SyncImport, Assert.Single(op.Audit.Events).Action);

        var sync = new SyncService(import, new FakeReferences(), new FakePeople(), anonymous.Guard, anonymous.Audit);
        await Assert.ThrowsAsync<AccessDeniedException>(() => sync.SyncAsync(new FakeSource(), external: true));
    }

    // ---- fakes ----

    private sealed class FakeReferences : IReferenceRepository
    {
        public int Writes { get; private set; }
        public bool Fail { get; set; }

        public Task<IReadOnlyList<UavType>> GetUavTypesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UavType>>(Array.Empty<UavType>());

        public Task<IReadOnlyList<TargetType>> GetTargetTypesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TargetType>>(Array.Empty<TargetType>());

        public Task<IReadOnlyList<MunitionType>> GetMunitionTypesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MunitionType>>(Array.Empty<MunitionType>());

        public Task<IReadOnlyList<Settlement>> GetSettlementsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Settlement>>(Array.Empty<Settlement>());

        public Task SaveAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem => Write();

        public Task DeleteAsync<T>(T item, CancellationToken ct = default) where T : ReferenceItem => Write();

        private Task Write()
        {
            if (Fail) throw new InvalidOperationException("boom");
            Writes++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePeople : IPersonRepository
    {
        public List<Person> Items { get; } = new();
        public int Writes { get; private set; }

        public Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Person>>(Items.ToList());

        public Task<Person?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Email == email));

        public Task SaveAsync(Person person, CancellationToken ct = default)
        {
            Writes++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Person person, CancellationToken ct = default)
        {
            Writes++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMissions : IMissionRepository
    {
        public List<Mission> Items { get; } = new();

        public Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Mission>>(Items.ToList());

        public Task<Mission?> GetByIdAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(m => m.Id == id));

        public Task<IReadOnlySet<string>> GetImportedKeysAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task AddRangeAsync(IEnumerable<Mission> missions, CancellationToken ct = default)
        {
            Items.AddRange(missions);
            return Task.CompletedTask;
        }

        public Task SaveAsync(Mission mission, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeJournal : IJournalRepository
    {
        public Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<JournalEntry>>(Array.Empty<JournalEntry>());

        public Task AddAsync(JournalEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSink : IResponseSink
    {
        public int Calls { get; private set; }

        public string Title => "fake";

        public Task<PublishResult> PublishAsync(PublishPayload payload, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new PublishResult(1, 0, 0, Array.Empty<string>()));
        }
    }

    private sealed class FakeSource : IResponseSource
    {
        public string Title => "fake";

        public Task<RawDirectories> LoadDirectoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(new RawDirectories());

        public Task<IReadOnlyList<RawResponseRow>> LoadResponsesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RawResponseRow>>(Array.Empty<RawResponseRow>());
    }
}
