namespace R2WAI.Application.Common.Exceptions;

/// <summary>
/// A required setting is missing or invalid — a server misconfiguration, never something the
/// caller can fix by retrying or changing their request. Distinct from InvalidOperationException,
/// which the exception middleware maps to 409 Conflict for legitimate domain-guard violations;
/// a config error must never be presented to a client as a retriable conflict.
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
}
