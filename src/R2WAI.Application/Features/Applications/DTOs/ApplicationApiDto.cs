namespace R2WAI.Application.Features.Applications.DTOs;

public class ApplicationApiDto
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string AuthScheme { get; init; } = "None";
    public string? CredentialRef { get; init; }

    // Never the decrypted secret itself — just enough for the edit dialog to show "configured"
    // without round-tripping the credential.
    public bool HasCredential { get; init; }
    public string? CredentialHeaderName { get; init; }
    public string? OpenApiSource { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
