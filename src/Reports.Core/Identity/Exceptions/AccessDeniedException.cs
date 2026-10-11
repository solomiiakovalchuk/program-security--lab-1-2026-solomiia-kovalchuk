using System.Runtime.Serialization;

namespace Reports.Core.Identity.Exceptions;

/// <summary>Thrown when the current role is not allowed to perform an action.</summary>
public class AccessDeniedException : Exception
{
    /// <summary>Creates an exception with a default message.</summary>
    public AccessDeniedException()
        : base("Access denied.")
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    public AccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public AccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

#pragma warning disable SYSLIB0051 // Standard exception constructor, required for the usual four-constructor set.
    /// <summary>Creates an exception from serialized data.</summary>
    protected AccessDeniedException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#pragma warning restore SYSLIB0051
}
