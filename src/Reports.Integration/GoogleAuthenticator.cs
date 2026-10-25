using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Services;
using Reports.Security;

namespace Reports.Integration;

/// <summary>
/// Помилка налаштування доступу до Google — показується користувачеві як підказка,
/// що саме треба донастроїти, а не як виняток бібліотеки.
/// </summary>
public sealed class GoogleAccessException : Exception
{
    public GoogleAccessException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Авторизація десктопного застосунку в Google (OAuth 2.0, тип «Desktop app»).
/// <para>
/// Перший запуск відкриває браузер і просить згоду; отриманий токен
/// кешується у теці <c>token</c>, далі згода більше не потрібна.
/// </para>
/// </summary>
public sealed class GoogleAuthenticator : IGoogleServiceFactory
{
        private readonly string _plainCredentialsPath;
    private readonly string _dataFolder;
    private readonly string _applicationName;
    private readonly IMasterKeyProvider _masterKeys;
    private readonly CredentialsVault _vault;

    /// <summary>Creates an authenticator that keeps credentials and tokens encrypted inside the data folder.</summary>
    public GoogleAuthenticator(string dataFolder, string applicationName, IMasterKeyProvider masterKeys)
    {
        _dataFolder = dataFolder;
        _plainCredentialsPath = Path.Combine(dataFolder, "credentials.json");
        _applicationName = ToHeaderSafe(applicationName);
        _masterKeys = masterKeys;
        _vault = new CredentialsVault(
            Path.Combine(dataFolder, "credentials.enc"),
            SecretProtectors.ForPurpose(masterKeys, SecretProtectors.GoogleClientSecretPurpose));
    }

    /// <summary>Where the user places the plaintext credentials.json (it is encrypted and removed on first use).</summary>
    public string CredentialsPath => _plainCredentialsPath;

    /// <summary>True when credentials are available, either imported already or waiting to be imported.</summary>
    public bool HasCredentialsFile => _vault.HasSecret || File.Exists(_plainCredentialsPath);

    /// <summary>Creates a Sheets service requesting only the scope the access level needs.</summary>
    public async Task<SheetsService> CreateServiceAsync(GoogleAccess access, CancellationToken ct = default)
    {
        if (!HasCredentialsFile)
            throw new GoogleAccessException(
                $"Не знайдено файл «{Path.GetFileName(_plainCredentialsPath)}».\n\n" +
                "Google Cloud Console → APIs & Services → Credentials → Create credentials → " +
                "OAuth client ID → тип «Desktop app» → Download JSON.\n" +
                $"Покладіть файл сюди:\n{_plainCredentialsPath}");

        UserCredential credential;
        try
        {
            if (File.Exists(_plainCredentialsPath))
                _vault.ImportPlaintext(_plainCredentialsPath);

            var store = new ProtectedDataStore(
                Path.Combine(_dataFolder, GoogleScopes.TokenFolderName(access)),
                SecretProtectors.ForPurpose(_masterKeys, SecretProtectors.GoogleOAuthTokenPurpose));

            using var stream = _vault.OpenSecrets();
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                GoogleScopes.For(access),
                user: "user",
                ct,
                store);
        }
        catch (GoogleAccessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GoogleAccessException(
                "Не вдалося авторизуватися в Google. Перевірте, що у файлі облікових даних " +
                "тип клієнта — «Desktop app», а ваш акаунт доданий у Test users на екрані згоди.",
                ex);
        }

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName
        });
    }

    /// <inheritdoc/>
    Task<SheetsService> IGoogleServiceFactory.CreateServiceAsync(GoogleAccess access, CancellationToken ct) =>
        CreateServiceAsync(access, ct);

    /// <summary>
    /// Прибирає з назви застосунку все, що не входить у ASCII.
    /// Google-клієнт підставляє її у заголовок User-Agent, а заголовки HTTP
    /// не допускають символів поза ASCII — кирилиця в назві валить будь-який
    /// запит із помилкою «The format of value ... is invalid».
    /// </summary>
    private static string ToHeaderSafe(string name)
    {
        var safe = new string((name ?? string.Empty)
            .Where(c => c > 31 && c < 127 && c != '(' && c != ')')
            .ToArray())
            .Trim();

        return safe.Length == 0 ? "ReportsWpf" : safe;
    }

    /// <summary>Забути збережені токени (читання й запис) — знадобиться при зміні акаунта.</summary>
    public void ForgetToken()
    {
        foreach (var access in new[] { GoogleAccess.ReadOnly, GoogleAccess.ReadWrite })
        {
            var folder = Path.Combine(_dataFolder, GoogleScopes.TokenFolderName(access));
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
