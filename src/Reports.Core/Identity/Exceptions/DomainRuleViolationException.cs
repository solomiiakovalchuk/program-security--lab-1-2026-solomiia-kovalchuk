using System.Runtime.Serialization;

namespace Reports.Core.Identity.Exceptions;

/// <summary>Thrown when an operation breaks a domain rule of the Identity context.</summary>
public class DomainRuleViolationException : Exception
{
    /// <summary>Creates an exception with a default message.</summary>
    public DomainRuleViolationException()
        : base("A domain rule was violated.")
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    public DomainRuleViolationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public DomainRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

#pragma warning disable SYSLIB0051 // Standard exception constructor, required for the usual four-constructor set.
    /// <summary>Creates an exception from serialized data.</summary>
    protected DomainRuleViolationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#pragma warning restore SYSLIB0051
}
