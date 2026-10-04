using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace R2WAI.Api.Logging;

/// <summary>
/// Catches sensitive fields nested inside objects logged via Serilog's "@" destructuring
/// operator (e.g. logger.LogInformation("Request: {@Request}", loginRequest)), which
/// SensitiveDataEnricher can't see — it only masks top-level LogEvent property names.
/// </summary>
public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    // Matched as a case-insensitive SUBSTRING of the property name, not an exact match --
    // exact-match missed real secret-bearing properties whose name embeds one of these terms as
    // part of a longer, more specific name (e.g. CreateApplicationApiCommand.CredentialSecret,
    // RegenerateWebhookKeyResultDto.RawKey), which fell through to Serilog's default destructuring
    // and got logged in plaintext despite this policy existing specifically to prevent that. A
    // safety net for secrets should fail toward over-masking an incidental non-secret field (e.g.
    // KeyPrefix) rather than under-masking a real one.
    private static readonly string[] SensitiveNameFragments =
    [
        "password", "secret", "apikey", "token", "credential", "rawkey",
        "encryptionkey", "creditcard", "ssn", "socialsecurity"
    ];

    internal static bool IsSensitiveName(string name) =>
        SensitiveNameFragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        result = null;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string or DateTime or DateTimeOffset or Guid or decimal)
            return false;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length == 0 || !properties.Any(p => IsSensitiveName(p.Name)))
            return false; // nothing sensitive here — let Serilog's default policies handle it

        var logProperties = new List<LogEventProperty>(properties.Length);
        foreach (var prop in properties)
        {
            if (prop.GetIndexParameters().Length > 0) continue;

            if (IsSensitiveName(prop.Name))
            {
                logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue("***MASKED***")));
                continue;
            }

            object? rawValue;
            try { rawValue = prop.GetValue(value); }
            catch { continue; }

            logProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(rawValue, destructureObjects: true)));
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }
}

public class SensitiveDataEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToMask = logEvent.Properties
            .Where(p => SensitiveDataDestructuringPolicy.IsSensitiveName(p.Key))
            .Select(p => p.Key)
            .ToList();

        foreach (var key in propertiesToMask)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "***MASKED***"));
        }
    }
}
