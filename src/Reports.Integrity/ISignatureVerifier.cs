namespace Reports.Integrity;

/// <summary>Verifies a digital signature over data.</summary>
public interface ISignatureVerifier
{
    /// <summary>Returns true if the signature is valid for the data.</summary>
    bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
}
