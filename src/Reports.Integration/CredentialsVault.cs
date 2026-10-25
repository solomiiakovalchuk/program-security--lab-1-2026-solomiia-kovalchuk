using System.Security.Cryptography;
using System.Text;
using Reports.Security;

namespace Reports.Integration;

/// <summary>Keeps the Google client secret (credentials.json) encrypted on disk and plaintext only in memory.</summary>
public sealed class CredentialsVault
{
    private static readonly byte[] Aad = Encoding.UTF8.GetBytes(SecretProtectors.GoogleClientSecretPurpose);

    private readonly string _encryptedPath;
    private readonly ISecretProtector _protector;

    /// <summary>Creates a vault that stores the encrypted file at the path.</summary>
    public CredentialsVault(string encryptedPath, ISecretProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPath);
        ArgumentNullException.ThrowIfNull(protector);
        _encryptedPath = encryptedPath;
        _protector = protector;
    }

    /// <summary>True when an encrypted client secret exists.</summary>
    public bool HasSecret => File.Exists(_encryptedPath);

    /// <summary>Encrypts a user-placed plaintext file, then overwrites and deletes it.</summary>
    public void ImportPlaintext(string plainPath)
    {
        if (!File.Exists(plainPath))
            throw new GoogleAccessException($"Не знайдено файл «{Path.GetFileName(plainPath)}».");

        var plain = File.ReadAllBytes(plainPath);
        try
        {
            var blob = _protector.Protect(plain, Aad);

            var directory = Path.GetDirectoryName(Path.GetFullPath(_encryptedPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temp = _encryptedPath + ".tmp";
            File.WriteAllBytes(temp, blob);
            File.Move(temp, _encryptedPath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        ShredAndDelete(plainPath);
    }

    /// <summary>Returns the decrypted client secret as an in-memory stream.</summary>
    public Stream OpenSecrets()
    {
        if (!HasSecret)
            throw new GoogleAccessException("Облікові дані Google ще не імпортовані.");

        try
        {
            return new MemoryStream(_protector.Unprotect(File.ReadAllBytes(_encryptedPath), Aad), writable: false);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new GoogleAccessException(
                "Файл облікових даних Google пошкоджений або змінений. " +
                "Покладіть credentials.json заново.", ex);
        }
    }

    private static void ShredAndDelete(string path)
    {
        try
        {
            var length = new FileInfo(path).Length;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                stream.Write(new byte[length]);
                stream.Flush(flushToDisk: true);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
