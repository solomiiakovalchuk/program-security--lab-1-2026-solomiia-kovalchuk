namespace Reports.Integrity;

/// <summary>Thrown when integrity inputs (manifest, key) cannot be used at all.</summary>
public sealed class IntegrityException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public IntegrityException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public IntegrityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
