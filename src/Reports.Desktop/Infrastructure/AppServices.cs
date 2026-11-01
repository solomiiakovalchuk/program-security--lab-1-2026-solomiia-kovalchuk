using System.IO;
using Reports.Audit;
using Reports.Core.Abstractions;
using Reports.Core.Aggregation;
using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Services;
using Reports.Data;
using Reports.Data.Repositories;
using Reports.Documents;
using Reports.Identity;
using Reports.Integration;
using Reports.Integrity;
using Reports.Security;

namespace Reports.Desktop.Infrastructure;

/// <summary>Holds the current session of the running application.</summary>
public sealed class SessionContext : ISessionContext
{
    /// <inheritdoc/>
    public Session? Current { get; set; }
}

/// <summary>
/// Корінь композиції: створює й зв'язує всі служби застосунку.
/// Зібрано вручну, без контейнера — залежностей небагато, і так видно
/// весь граф на одному екрані.
/// <para>
/// Публічні властивості <see cref="References"/>, <see cref="People"/>, <see cref="Missions"/>
/// і <see cref="Journal"/> — це охоронювані обгортки (SEC-04); сирі репозиторії доступні лише службам.
/// </para>
/// </summary>
public sealed class AppServices
{
    private const int AuditRetentionDays = 365;

    private AppServices(string dataFolder, StartupIntegrityResult integrity)
    {
        DataFolder = dataFolder;
        IntegrityResult = integrity;

        // SEC-12: тека даних і база доступні лише власнику профілю; помилка не валить застосунок.
        TryProtect(dataFolder);

        var settingsStore = new SettingsStore(Path.Combine(dataFolder, "settings.json"));
        var settings = settingsStore.Load();
        if (string.IsNullOrWhiteSpace(settings.OutputFolder))
            settings.OutputFolder = Path.Combine(dataFolder, "Рапорти");
        Settings = settings;
        SettingsStore = settingsStore;

        ContextFactory = new SqliteContextFactory(Path.Combine(dataFolder, "reports.db"));

        // Сесія, політика доступу, журнал аудиту.
        Session = new SessionContext();
        Policy = new AccessPolicyService();
        var auditRepository = new AuditRepository(ContextFactory);
        Audit = new AuditRecordingService(auditRepository, Session, SystemClock.Instance, ex => ErrorLog.Write("Помилка журналу аудиту", ex));
        Guard = new AccessGuard(Session, Policy, Audit);
        AuditVerifier = new AuditChainVerifier(auditRepository);
        AuditQuery = new AuditQueryService(auditRepository, Session, Policy, AuditVerifier, Audit);
        AuditExport = new AuditExportService(auditRepository, Session, Policy, Audit, SystemClock.Instance);
        AuditRetention = new AuditRetentionService(auditRepository, Session, Policy, Audit, SystemClock.Instance, AuditRetentionDays);

        // Облікові записи.
        PasswordHasher = new PasswordHasher();
        Accounts = new AccountRepository(ContextFactory);
        Authentication = new AuthenticationService(Accounts, PasswordHasher, Session, null, Audit);
        ChangePassword = new ChangePasswordService(Audit);
        AccountManagement = new AccountManagementService(Accounts, PasswordHasher, Session, Policy, Audit);
        SessionService = new SessionService(Session, Audit);

        // Дані: сирі репозиторії лише для служб, назовні — охоронювані.
        var rawReferences = new ReferenceRepository(ContextFactory);
        var rawPeople = new PersonRepository(ContextFactory);
        var rawMissions = new MissionRepository(ContextFactory);
        var rawJournal = new JournalRepository(ContextFactory);

        References = new GuardedReferenceRepository(rawReferences, Guard, Audit);
        People = new GuardedPersonRepository(rawPeople, Guard, Audit);
        Missions = new GuardedMissionRepository(rawMissions, Guard);
        Journal = new GuardedJournalRepository(rawJournal, Guard);

        Renderer = new DocxReportRenderer(settings, Path.Combine(dataFolder, "template.docx"));

        // SEC-05/06: секрети Google лише в зашифрованому вигляді, ключ захищений DPAPI.
        var masterKeys = new DpapiMasterKeyProvider(Path.Combine(dataFolder, "master.key"));
        // Лише ASCII: з цієї назви складається заголовок User-Agent.
        Authenticator = new GoogleAuthenticator(dataFolder, "ReportsWpf", masterKeys);

        SpreadsheetInfo = new GoogleSpreadsheetInfoProvider(Authenticator);
        Confirmation = new SpreadsheetConfirmation(
            settings, SpreadsheetInfo,
            (title, id) => ConfirmSpreadsheet(title, id),
            Audit,
            () => SettingsStore.Save(settings));

        Reports = new ReportService(
            rawMissions, rawJournal, ReportAggregator.CreateDefault(),
            new IReportRenderer[] { Renderer }, settings, Guard, Audit);
        Import = new ImportService(rawMissions, rawReferences, null, Guard, Audit);
        Sync = new SyncService(Import, rawReferences, rawPeople, Guard, Audit);
        Publish = new PublishService(rawReferences, rawPeople, rawMissions, Guard, Audit);
        MissionStatus = new MissionStatusService(rawMissions, Guard, Audit);
        SettingsService = new SettingsService(
            settings, () => SettingsStore.Save(settings), Guard, Audit, Confirmation.OnSpreadsheetIdChanged);
        RebuildTemplateAction = GuardedAction.RebuildTemplate(Renderer.RebuildTemplate, Guard, Audit);
        ForgetTokenAction = GuardedAction.ForgetToken(Authenticator.ForgetToken, Guard, Audit);
        Connection = new GoogleConnectionService(Authenticator, settings, Guard, Audit);
    }

    public AppSettings Settings { get; }
    public SettingsStore SettingsStore { get; }
    public SqliteContextFactory ContextFactory { get; }
    public string DataFolder { get; }

    /// <summary>Поточна сесія (null — ніхто не увійшов).</summary>
    public ISessionContext Session { get; }
    public IAccessPolicy Policy { get; }
    public AccessGuard Guard { get; }

    public IAuditSink Audit { get; }
    public AuditQueryService AuditQuery { get; }
    public AuditExportService AuditExport { get; }
    public AuditRetentionService AuditRetention { get; }
    public AuditChainVerifier AuditVerifier { get; }

    public IPasswordHasher PasswordHasher { get; }
    public IAccountRepository Accounts { get; }
    public AuthenticationService Authentication { get; }
    public ChangePasswordService ChangePassword { get; }
    public AccountManagementService AccountManagement { get; }
    public SessionService SessionService { get; }

    public IReferenceRepository References { get; }
    public IPersonRepository People { get; }
    public IMissionRepository Missions { get; }
    public IJournalRepository Journal { get; }

    public ImportService Import { get; }
    public SyncService Sync { get; }
    public PublishService Publish { get; }
    public ReportService Reports { get; }
    public MissionStatusService MissionStatus { get; }
    public SettingsService SettingsService { get; }
    public GuardedAction RebuildTemplateAction { get; }
    public GuardedAction ForgetTokenAction { get; }
    public GoogleConnectionService Connection { get; }
    public DocxReportRenderer Renderer { get; }
    public GoogleAuthenticator Authenticator { get; }
    public ISpreadsheetInfoProvider SpreadsheetInfo { get; }
    public SpreadsheetConfirmation Confirmation { get; }

    /// <summary>
    /// Запит підтвердження таблиці (назва, id) → так/ні. Постачає інтерфейс;
    /// доти відповідь «ні», тобто запис у таблицю відхиляється (SEC-14).
    /// </summary>
    public Func<string, string, Task<bool>> ConfirmSpreadsheet { get; set; } = (_, _) => Task.FromResult(false);

    /// <summary>Результат перевірки цілісності під час запуску (SEC-16).</summary>
    public StartupIntegrityResult IntegrityResult { get; }

    public StartupIntegrityOutcome IntegrityState => IntegrityResult.Outcome;

    /// <summary>True для непідписаної збірки: інтерфейс показує застереження.</summary>
    public bool IntegrityWarning => IntegrityState == StartupIntegrityOutcome.NoManifest;

    public string TemplatePath => Renderer.TemplatePath;
    public string CredentialsPath => Authenticator.CredentialsPath;

    /// <summary>Джерело даних відповідно до налаштувань: демонстраційне або Google.</summary>
    public IResponseSource CreateResponseSource() =>
        Settings.UseDemoData
            ? new DemoResponseSource()
            : new GoogleSheetsResponseSource(Authenticator, Settings);

    /// <summary>
    /// Приймач для зворотного запису. У демонстраційному режимі його немає:
    /// вигадані дані нікуди вивантажувати.
    /// </summary>
    public IResponseSink? CreateResponseSink() =>
        Settings.UseDemoData ? null : new GoogleSheetsWriter(Authenticator, Settings, Confirmation, Guard);

    public static AppServices Create(StartupIntegrityResult integrity)
    {
        var dataFolder = AppDataFolder.Resolve();
        Directory.CreateDirectory(dataFolder);
        return new AppServices(dataFolder, integrity);
    }

    /// <summary>
    /// Створює й мігрує базу, гарантує вбудованого адміністратора і шаблон, записує
    /// початкові події аудиту. Якщо перевірка цілісності провалена — лише записує подію.
    /// </summary>
    public async Task InitializeAsync()
    {
        await DatabaseInitializer.InitializeAsync(ContextFactory);
        TryProtect(ContextFactory.DatabasePath);

        if (IntegrityState == StartupIntegrityOutcome.Failed)
        {
            Audit.Record(
                AuditAction.IntegrityCheckFailed, AuditTarget.Application, null, AuditOutcome.Failure,
                new Dictionary<string, string>
                {
                    ["reason"] = IntegrityResult.Reason,
                    ["count"] = IntegrityResult.ProblemCount.ToString()
                });
            return;
        }

        await new BootstrapService().EnsureBuiltInAdminAsync(Accounts);
        Renderer.EnsureTemplateExists();

        if (IntegrityWarning)
        {
            Audit.Record(
                AuditAction.IntegrityUnsigned, AuditTarget.Application, null, AuditOutcome.Success,
                new Dictionary<string, string> { ["reason"] = IntegrityResult.Reason });
        }

        Audit.Record(
            AuditAction.ApplicationStarted, AuditTarget.Application, null, AuditOutcome.Success,
            new Dictionary<string, string>
            {
                ["version"] = typeof(AppServices).Assembly.GetName().Version?.ToString() ?? "unknown"
            });
    }

    private static void TryProtect(string path)
    {
        try
        {
            if (Directory.Exists(path) || File.Exists(path))
                OwnerOnlyAcl.Apply(path);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Не вдалося обмежити доступ до " + Path.GetFileName(path), ex);
        }
    }
}
