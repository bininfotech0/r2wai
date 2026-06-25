using Serilog.Core;
using Serilog.Events;

namespace R2WAI.Api.Logging;

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
        return false;
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
