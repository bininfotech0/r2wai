using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using R2WAI.Domain.Entities;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        // Authentication resolves a key by its SHA-256 hash on every API-key request.
        builder.HasIndex(key => key.KeyHash);
    }
}
