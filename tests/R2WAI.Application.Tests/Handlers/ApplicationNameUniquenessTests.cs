using System.Linq.Expressions;
using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// Connected-system names are unique per tenant, case-insensitively and after trimming, on every
/// path that can set a name: create, update and version rollback. The repository mock evaluates the
/// handler's real predicate against an in-memory set, so the query itself is under test, not just
/// the handler's reaction to a canned result.
/// </summary>
public class ApplicationNameUniquenessTests
{
    private readonly Mock<IRepository<ConnectedApplication>> _applicationRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly List<ConnectedApplication> _existing = [];

    public ApplicationNameUniquenessTests()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _applicationRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<ConnectedApplication, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns<Expression<Func<ConnectedApplication, bool>>, CancellationToken>(
                (predicate, _) => Task.FromResult(_existing.AsQueryable().FirstOrDefault(predicate)));
        _applicationRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, _) => Task.FromResult(_existing.FirstOrDefault(a => a.Id == id)));
    }

    private ConnectedApplication Seed(string name, string code)
    {
        var app = new ConnectedApplication(Guid.NewGuid(), _tenantId, null, name, code);
        _existing.Add(app);
        return app;
    }

    private CreateApplicationCommandHandler CreateHandler() =>
        new(_applicationRepoMock.Object, new Mock<IRepository<Department>>().Object, _uowMock.Object,
            _currentUserMock.Object, _mapperMock.Object);

    private UpdateApplicationCommandHandler UpdateHandler() =>
        new(_applicationRepoMock.Object, _uowMock.Object, _mapperMock.Object);

    [Theory]
    [InlineData("HR Portal")]
    [InlineData("hr portal")]
    [InlineData("  HR PORTAL  ")]
    public async Task Create_NameAlreadyUsed_IgnoringCaseAndWhitespace_Throws(string name)
    {
        Seed("HR Portal", "HR");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(
            new CreateApplicationCommand { Name = name, Code = "HR2" }, CancellationToken.None));

        Assert.Contains(ex.Errors, kv => kv.Key == "Name");
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_CodeDiffersOnlyByCase_Throws()
    {
        Seed("HR Portal", "HR");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(
            new CreateApplicationCommand { Name = "Payroll", Code = "hr" }, CancellationToken.None));

        Assert.Contains(ex.Errors, kv => kv.Key == nameof(CreateApplicationCommand.Code));
    }

    [Fact]
    public async Task Create_TrimsNameAndCode()
    {
        ConnectedApplication? added = null;
        _applicationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConnectedApplication>(), It.IsAny<CancellationToken>()))
            .Callback<ConnectedApplication, CancellationToken>((a, _) => added = a)
            .Returns<ConnectedApplication, CancellationToken>((a, _) => Task.FromResult(a));

        await CreateHandler().Handle(new CreateApplicationCommand { Name = "  Payroll ", Code = " PAY " }, CancellationToken.None);

        Assert.Equal("Payroll", added!.Name);
        Assert.Equal("PAY", added.Code);
    }

    [Fact]
    public async Task Update_RenameToAnotherSystemsName_Throws()
    {
        Seed("HR Portal", "HR");
        var payroll = Seed("Payroll", "PAY");

        await Assert.ThrowsAsync<ValidationException>(() => UpdateHandler().Handle(
            new UpdateApplicationCommand { Id = payroll.Id, Name = "hr portal" }, CancellationToken.None));

        Assert.Equal("Payroll", payroll.Name);
    }

    [Fact]
    public async Task Update_KeepingOwnName_OrChangingItsCase_Succeeds()
    {
        var hr = Seed("HR Portal", "HR");

        await UpdateHandler().Handle(new UpdateApplicationCommand { Id = hr.Id, Name = "HR portal" }, CancellationToken.None);

        Assert.Equal("HR portal", hr.Name);
    }

    [Fact]
    public async Task Rollback_ToNameNowTakenByAnotherSystem_Throws()
    {
        var current = Seed("Property Tax Portal", "PROP");
        Seed("Property Tax", "PT");

        const string snapshotJson =
            """{"Name":"Property Tax","Description":null,"BaseUrl":null,"Environment":"Development","Apis":[],"Configuration":null}""";
        var version = ApplicationVersion.CreateSnapshot(Guid.NewGuid(), _tenantId, current.Id, 1, snapshotJson);

        var versionRepo = new Mock<IRepository<ApplicationVersion>>();
        versionRepo.Setup(r => r.GetByIdAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);

        var handler = new RollbackApplicationVersionCommandHandler(
            versionRepo.Object, new Mock<IRepository<ApplicationApi>>().Object,
            new Mock<IRepository<ApplicationConfiguration>>().Object, _applicationRepoMock.Object,
            _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new RollbackApplicationVersionCommand { VersionId = version.Id }, CancellationToken.None));

        Assert.Equal("Property Tax Portal", current.Name);
    }
}
