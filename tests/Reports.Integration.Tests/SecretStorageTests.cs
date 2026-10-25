using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Reports.Integration;
using Reports.Security;

namespace Reports.Integration.Tests;

public class SecretStorageTests
{
    private const string TokenText = "ya29.SUPER-SECRET-ACCESS-TOKEN";
    private const string RefreshText = "1//SUPER-SECRET-REFRESH-TOKEN";
    private const string SecretText = "GOCSPX-super-secret-client-value";

    private static string AllFilesText(string folder) =>
        string.Concat(Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Encoding.UTF8.GetString(File.ReadAllBytes(f))));

    private static ISecretProtector Protector(string purpose) =>
        SecretProtectors.ForPurpose(new FakeMasterKeyProvider(), purpose);

    [Fact]
    public async Task Token_files_do_not_contain_token_text_and_round_trip()
    {
        using var temp = new TempFolder();
        var store = new ProtectedDataStore(temp.Path, Protector(SecretProtectors.GoogleOAuthTokenPurpose));

        await store.StoreAsync("user", new TokenResponse { AccessToken = TokenText, RefreshToken = RefreshText });

        var text = AllFilesText(temp.Path);
        Assert.DoesNotContain(TokenText, text);
        Assert.DoesNotContain(RefreshText, text);
        Assert.DoesNotContain("access_token", text);

        var back = await store.GetAsync<TokenResponse>("user");
        Assert.Equal(TokenText, back.AccessToken);
        Assert.Equal(RefreshText, back.RefreshToken);
    }

    [Fact]
    public async Task Missing_token_returns_default_and_delete_removes_it()
    {
        using var temp = new TempFolder();
        var store = new ProtectedDataStore(temp.Path, Protector("google-oauth-token"));

        Assert.Null(await store.GetAsync<TokenResponse>("user"));

        await store.StoreAsync("user", new TokenResponse { AccessToken = TokenText });
        await store.DeleteAsync<TokenResponse>("user");
        Assert.Null(await store.GetAsync<TokenResponse>("user"));
    }

    [Fact]
    public async Task Tampered_token_file_is_rejected_with_a_clean_error()
    {
        using var temp = new TempFolder();
        var store = new ProtectedDataStore(temp.Path, Protector("google-oauth-token"));
        await store.StoreAsync("user", new TokenResponse { AccessToken = TokenText });

        var file = Directory.GetFiles(temp.Path).Single();
        var bytes = File.ReadAllBytes(file);
        bytes[bytes.Length / 2] ^= 0xFF;
        File.WriteAllBytes(file, bytes);

        var ex = await Assert.ThrowsAsync<GoogleAccessException>(() => store.GetAsync<TokenResponse>("user"));
        Assert.DoesNotContain(TokenText, ex.Message);
    }

    [Fact]
    public async Task Token_encrypted_for_another_purpose_is_rejected()
    {
        using var temp = new TempFolder();
        var master = new FakeMasterKeyProvider();
        var writer = new ProtectedDataStore(temp.Path, SecretProtectors.ForPurpose(master, "google-oauth-token"));
        var reader = new ProtectedDataStore(temp.Path, SecretProtectors.ForPurpose(master, "google-client-secret"));
        await writer.StoreAsync("user", new TokenResponse { AccessToken = TokenText });

        await Assert.ThrowsAsync<GoogleAccessException>(() => reader.GetAsync<TokenResponse>("user"));
    }

    private static string WriteCredentials(string folder)
    {
        var path = Path.Combine(folder, "credentials.json");
        File.WriteAllText(path,
            "{\"installed\":{\"client_id\":\"id.apps.googleusercontent.com\",\"client_secret\":\"" + SecretText +
            "\",\"redirect_uris\":[\"http://localhost\"],\"auth_uri\":\"https://accounts.google.com/o/oauth2/auth\"," +
            "\"token_uri\":\"https://oauth2.googleapis.com/token\"}}");
        return path;
    }

    [Fact]
    public void Credentials_are_encrypted_plaintext_is_removed_and_secret_round_trips_in_memory()
    {
        using var temp = new TempFolder();
        var plain = WriteCredentials(temp.Path);
        var encrypted = Path.Combine(temp.Path, "credentials.enc");
        var vault = new CredentialsVault(encrypted, Protector(SecretProtectors.GoogleClientSecretPurpose));

        vault.ImportPlaintext(plain);

        Assert.False(File.Exists(plain));
        Assert.True(vault.HasSecret);
        Assert.DoesNotContain(SecretText, AllFilesText(temp.Path));
        Assert.DoesNotContain("client_secret", AllFilesText(temp.Path));

        using var stream = vault.OpenSecrets();
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        Assert.Equal(SecretText, secrets.ClientSecret);
        Assert.Equal("id.apps.googleusercontent.com", secrets.ClientId);
    }

    [Fact]
    public void Tampered_credentials_file_is_rejected_with_a_clean_error()
    {
        using var temp = new TempFolder();
        var plain = WriteCredentials(temp.Path);
        var encrypted = Path.Combine(temp.Path, "credentials.enc");
        var vault = new CredentialsVault(encrypted, Protector(SecretProtectors.GoogleClientSecretPurpose));
        vault.ImportPlaintext(plain);

        var bytes = File.ReadAllBytes(encrypted);
        bytes[^3] ^= 0x01;
        File.WriteAllBytes(encrypted, bytes);

        var ex = Assert.Throws<GoogleAccessException>(() => vault.OpenSecrets());
        Assert.DoesNotContain(SecretText, ex.Message);
    }

    [Fact]
    public void Authenticator_sees_credentials_waiting_for_import_and_imported_ones()
    {
        using var temp = new TempFolder();
        var auth = new GoogleAuthenticator(temp.Path, "Test", new FakeMasterKeyProvider());
        Assert.False(auth.HasCredentialsFile);

        WriteCredentials(temp.Path);
        Assert.True(auth.HasCredentialsFile);
    }
}
