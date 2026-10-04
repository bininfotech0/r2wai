using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AgentFrameworkRuntime's own config guard — the one piece of this class testable without
/// a real OpenAI credential, which does not exist in this environment (confirmed: the running dev
/// stack's AI__OpenAI__ApiKey is set but empty). Actual chat/tool-calling behavior against a real
/// model is NOT verified by an automated test here — see docs/architecture/AI-RUNTIME.md for the
/// live-verification status this leaves open, matching plan Phase 4's own "live-verify with the
/// flag flipped in the real Docker stack" bar, which needs a real key to execute.
/// </summary>
public class AgentFrameworkRuntimeTests
{
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? TenantId => Guid.NewGuid();
        public string[] Roles => [];
        public bool IsAuthenticated => true;
        public string? IpAddress => null;
        public string? CorrelationId => null;
    }

    private static AgentFrameworkRuntime CreateSut(IConfiguration? configuration = null)
    {
        var toolFactory = new MafToolFunctionFactory(
            new NullRepo(), null!, null!, new FakeCurrentUserService(), NullLogger<MafToolFunctionFactory>.Instance);
        return new AgentFrameworkRuntime(configuration ?? new ConfigurationBuilder().Build(), toolFactory, new ChatTraceCollector());
    }

    private sealed class NullRepo : R2WAI.Domain.Interfaces.IRepository<R2WAI.Domain.Entities.ToolDefinition>
    {
        public Task<R2WAI.Domain.Entities.ToolDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition?> GetByIdAsync(Guid id, System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, object>> include, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>)Array.Empty<R2WAI.Domain.Entities.ToolDefinition>());
        public Task<IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, string includePath, CancellationToken ct = default) => FindAsync(predicate, ct);
        public Task<R2WAI.Domain.Entities.ToolDefinition?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition> AddAsync(R2WAI.Domain.Entities.ToolDefinition entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(R2WAI.Domain.Entities.ToolDefinition entity) => throw new NotImplementedException();
        public void Delete(R2WAI.Domain.Entities.ToolDefinition entity) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task InvokeAsync_NoApiKeyConfigured_ThrowsClearConfigurationError_NeverAttemptsAConnection()
    {
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InvokeAsync(new AgentRuntimeRequest("hello")));

        Assert.Contains("AI:OpenAI:ApiKey", ex.Message);
    }

    [Fact]
    public async Task StreamAsync_NoApiKeyConfigured_ThrowsOnFirstMoveNext_NeverAttemptsAConnection()
    {
        var sut = CreateSut();

        async Task ConsumeFirstChunk()
        {
            await foreach (var _ in sut.StreamAsync(new AgentRuntimeRequest("hello"))) { }
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(ConsumeFirstChunk);
        Assert.Contains("AI:OpenAI:ApiKey", ex.Message);
    }
}
