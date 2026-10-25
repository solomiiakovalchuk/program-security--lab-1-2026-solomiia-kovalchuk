using System.Security.Cryptography;
using System.Text;
using Google.Apis.Json;
using Google.Apis.Util.Store;
using Reports.Security;

namespace Reports.Integration;

/// <summary>Google IDataStore that keeps every value encrypted (AES-256-GCM) in its own file.</summary>
public sealed class ProtectedDataStore : IDataStore
{
    private readonly string _folder;
    private readonly ISecretProtector _protector;

    /// <summary>Creates a store in the folder; the protector should use the google-oauth-token purpose.</summary>
    public ProtectedDataStore(string folder, ISecretProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(protector);
        _folder = folder;
        _protector = protector;
    }

    /// <inheritdoc/>
    public Task StoreAsync<T>(string key, T value)
    {
        var name = FileName<T>(key);
        var json = NewtonsoftJsonSerializer.Instance.Serialize(value);
        var blob = _protector.Protect(Encoding.UTF8.GetBytes(json), Encoding.UTF8.GetBytes(name));

        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, name);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, blob);
        File.Move(temp, path, overwrite: true);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync<T>(string key)
    {
        var path = Path.Combine(_folder, FileName<T>(key));
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<T> GetAsync<T>(string key)
    {
        var name = FileName<T>(key);
        var path = Path.Combine(_folder, name);
        if (!File.Exists(path)) return Task.FromResult(default(T)!);

        var blob = File.ReadAllBytes(path);
        byte[] plain;
        try
        {
            plain = _protector.Unprotect(blob, Encoding.UTF8.GetBytes(name));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new GoogleAccessException(
                "Збережений токен Google пошкоджений або змінений. " +
                "Натисніть «Забути токен» і авторизуйтеся знову.", ex);
        }

        return Task.FromResult(NewtonsoftJsonSerializer.Instance.Deserialize<T>(Encoding.UTF8.GetString(plain)));
    }

    /// <inheritdoc/>
    public Task ClearAsync()
    {
        if (Directory.Exists(_folder))
        {
            foreach (var file in Directory.GetFiles(_folder)) File.Delete(file);
        }

        return Task.CompletedTask;
    }

    private static string FileName<T>(string key) =>
        Uri.EscapeDataString($"{typeof(T).FullName}-{key}") + ".bin";
}
