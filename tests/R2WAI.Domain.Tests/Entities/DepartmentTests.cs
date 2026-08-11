namespace R2WAI.Domain.Tests.Entities;

public class DepartmentTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var department = new Department(id, tenantId, "Revenue", "REV", "Property tax and collections");

        Assert.Equal(id, department.Id);
        Assert.Equal(tenantId, department.TenantId);
        Assert.Equal("Revenue", department.Name);
        Assert.Equal("REV", department.Code);
        Assert.Equal("Property tax and collections", department.Description);
        Assert.True(department.IsActive);
        Assert.Null(department.HeadUserId);
        Assert.Empty(department.Applications);
    }

    [Fact]
    public void UpdateDetails_ChangesNameAndDescription()
    {
        var department = new Department(Guid.NewGuid(), Guid.NewGuid(), "Revenue", "REV");

        department.UpdateDetails("Revenue Services", "Updated description");

        Assert.Equal("Revenue Services", department.Name);
        Assert.Equal("Updated description", department.Description);
        Assert.NotNull(department.ModifiedAt);
    }

    [Fact]
    public void SetHead_AssignsHeadUser()
    {
        var department = new Department(Guid.NewGuid(), Guid.NewGuid(), "Revenue", "REV");
        var userId = Guid.NewGuid();

        department.SetHead(userId);

        Assert.Equal(userId, department.HeadUserId);
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var department = new Department(Guid.NewGuid(), Guid.NewGuid(), "Revenue", "REV");

        department.Deactivate();
        Assert.False(department.IsActive);

        department.Activate();
        Assert.True(department.IsActive);
    }
}
