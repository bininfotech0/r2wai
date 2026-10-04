using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// Covers the R2WAI 2.0 product decision that a connected system must be creatable without an
/// organisational container. The substantive change was removing the mandatory
/// <c>RuleFor(v =&gt; v.DepartmentId).NotEmpty()</c> rule; the security-relevant half is that a
/// *supplied* department id is still validated against the caller's tenant, so relaxing the rule
/// did not quietly open a cross-tenant foreign key.
/// </summary>
public class CreateApplicationCommandHandlerTests
{
    private readonly Mock<IRepository<ConnectedApplication>> _applicationRepoMock = new();
    private readonly Mock<IRepository<Department>> _departmentRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _departmentId = Guid.NewGuid();

    public CreateApplicationCommandHandlerTests()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
    }

    private CreateApplicationCommandHandler CreateHandler() =>
        new(_applicationRepoMock.Object, _departmentRepoMock.Object, _uowMock.Object,
            _currentUserMock.Object, _mapperMock.Object);

    private static CreateApplicationCommand Command(Guid? departmentId) => new()
    {
        DepartmentId = departmentId,
        Name = "Supplier Portal",
        Code = "SUPPLIER",
        Environment = ApplicationEnvironment.Development
    };

    [Fact]
    public async Task Handle_NoDepartment_CreatesApplicationWithNullDepartment()
    {
        var handler = CreateHandler();
        ConnectedApplication? added = null;
        _applicationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()))
            .Callback<ConnectedApplication, CancellationToken>((a, _) => added = a)
            .Returns<ConnectedApplication, CancellationToken>((a, _) => Task.FromResult(a));

        await handler.Handle(Command(null), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Null(added!.DepartmentId);
        Assert.Equal(_tenantId, added.TenantId);
        Assert.Equal("Supplier Portal", added.Name);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateCodeInSameScope_ThrowsValidationError()
    {
        var handler = CreateHandler();
        var existing = new ConnectedApplication(
            Guid.NewGuid(), _tenantId, null, "Existing Supplier", "SUPPLIER");
        _applicationRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ConnectedApplication, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(Command(null), CancellationToken.None));

        Assert.Contains(ex.Errors, kv => kv.Key == nameof(CreateApplicationCommand.Code));
        _applicationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UniqueCodeInSameScope_CreatesApplication()
    {
        var handler = CreateHandler();
        _applicationRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ConnectedApplication, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConnectedApplication?)null);
        _applicationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()))
            .Returns<ConnectedApplication, CancellationToken>((a, _) => Task.FromResult(a));

        await handler.Handle(Command(null), CancellationToken.None);

        _applicationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoDepartment_DoesNotLookUpAnyDepartment()
    {
        var handler = CreateHandler();
        _applicationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()))
            .Returns<ConnectedApplication, CancellationToken>((a, _) => Task.FromResult(a));

        await handler.Handle(Command(null), CancellationToken.None);

        _departmentRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_KnownDepartmentInTenant_KeepsTheDepartment()
    {
        var handler = CreateHandler();
        var department = new Department(_departmentId, _tenantId, "Revenue", "REV");
        _departmentRepoMock
            .Setup(r => r.GetByIdAsync(_departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(department);

        ConnectedApplication? added = null;
        _applicationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()))
            .Callback<ConnectedApplication, CancellationToken>((a, _) => added = a)
            .Returns<ConnectedApplication, CancellationToken>((a, _) => Task.FromResult(a));

        await handler.Handle(Command(_departmentId), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(_departmentId, added!.DepartmentId);
    }

    [Fact]
    public async Task Handle_DepartmentFromAnotherTenant_ThrowsNotFound()
    {
        var handler = CreateHandler();
        var foreignDepartment = new Department(_departmentId, Guid.NewGuid(), "Other", "OTH");
        _departmentRepoMock
            .Setup(r => r.GetByIdAsync(_departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignDepartment);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(Command(_departmentId), CancellationToken.None));

        _applicationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownDepartment_ThrowsNotFound()
    {
        var handler = CreateHandler();
        _departmentRepoMock
            .Setup(r => r.GetByIdAsync(_departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(Command(_departmentId), CancellationToken.None));

        _applicationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Validator_AllowsMissingDepartment()
    {
        var result = new CreateApplicationCommandValidator().Validate(Command(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_StillRequiresNameAndCode()
    {
        var result = new CreateApplicationCommandValidator().Validate(new CreateApplicationCommand
        {
            DepartmentId = null,
            Name = string.Empty,
            Code = string.Empty
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateApplicationCommand.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateApplicationCommand.Code));
    }
}
