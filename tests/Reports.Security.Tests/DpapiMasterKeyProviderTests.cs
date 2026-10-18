namespace Reports.Security.Tests;

/// <summary>
/// Tests for <see cref="DpapiMasterKeyProvider"/>. Windows only (DPAPI). Uses a temporary file that is deleted afterwards.
/// </summary>
public sealed class DpapiMasterKeyProviderTests
{
    [Fact]
    public void DpapiMasterKeyProvider_PersistsAndReloads_SameKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var keyFilePath = Path.GetTempFileName();
        File.Delete(keyFilePath);

        try
        {
            var first = new DpapiMasterKeyProvider(keyFilePath).GetOrCreateMasterKey();
            var second = new DpapiMasterKeyProvider(keyFilePath).GetOrCreateMasterKey();

            Assert.Equal(32, first.Length);
            Assert.Equal(first, second);
            Assert.True(File.Exists(keyFilePath));
        }
        finally
        {
            if (File.Exists(keyFilePath))
            {
                File.Delete(keyFilePath);
            }
        }
    }
}
