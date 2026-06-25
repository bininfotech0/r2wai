namespace R2WAI.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var user = new User(id, tenantId, "ext-123", "test@example.com", "John", "Doe");

        Assert.Equal(id, user.Id);
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal("ext-123", user.ExternalId);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }

    [Fact]
    public void UpdateProfile_ChangesNameAndAvatar()
    {
        var user = CreateDefault();
        user.UpdateProfile("Jane", "Smith", "https://avatar.url");

        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.Equal("https://avatar.url", user.AvatarUrl);
    }

    [Fact]
    public void SetPasswordHash_SetsHash()
    {
        var user = CreateDefault();
        user.SetPasswordHash("hashed_password_123");
        Assert.Equal("hashed_password_123", user.PasswordHash);
    }

    [Fact]
    public void SetLastLogin_UpdatesTimestamp()
    {
        var user = CreateDefault();
        user.SetLastLogin();
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public void SoftDelete_SetsIsDeleted()
    {
        var user = CreateDefault();
        user.SoftDelete();
        Assert.True(user.IsDeleted);
    }

    private static User CreateDefault()
    {
        return new User(Guid.NewGuid(), Guid.NewGuid(), "ext-1", "test@test.com", "Test", "User");
    }
}
