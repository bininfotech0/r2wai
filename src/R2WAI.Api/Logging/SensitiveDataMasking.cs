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
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordHash", "secret", "secretKey", "apiKey",
        "token", "accessToken", "refreshToken", "refreshTokenHash",
        "smtpPassword", "clientSecret", "encryptionKey",
        "creditCard", "ssn", "socialSecurity"
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        result = null;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string or DateTime or DateTimeOffset or Guid or decimal)
            return false;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length == 0 || !properties.Any(p => SensitiveFields.Contains(p.Name)))
            return false; // nothing sensitive here — let Serilog's default policies handle it

        var logProperties = new List<LogEventProperty>(properties.Length);
        foreach (var prop in properties)
        {
            if (prop.GetIndexParameters().Length > 0) continue;

            if (SensitiveFields.Contains(prop.Name))
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
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "PasswordHash", "SecretKey", "ApiKey", "Token",
        "AccessToken", "RefreshToken", "RefreshTokenHash",
        "SmtpPassword", "ClientSecret", "EncryptionKey"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToMask = logEvent.Properties
            .Where(p => SensitivePropertyNames.Contains(p.Key))
            .Select(p => p.Key)
            .ToList();

        foreach (var key in propertiesToMask)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "***MASKED***"));
        }
    }
}
