namespace Reports.Security;

/// <summary>Provides the application master key.</summary>
public interface IMasterKeyProvider
{
    /// <summary>Returns the master key, creating it on first use.</summary>
    byte[] GetOrCreateMasterKey();
}
