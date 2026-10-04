using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AgentRuntimeSelector's own routing (implementation plan Phase 4) — the property under
/// test is that it actually dispatches to the runtime the tenant's policy names, not that either
/// backing runtime works end to end (AgentRuntimeTests covers the SK side; there is no live OpenAI
/// credential in this environment to exercise the MAF side against a real model — see
/// AgentFrameworkRuntimeTests for what is covered there instead).
///
/// Distinguishes the two branches by an externally observable difference rather than a mock
/// expectation on the concrete classes (the selector depends on them directly, not through a
/// second IAgentRuntime seam): AgentRuntime here is a real instance over a fake IAIService that
/// returns a canned reply with no exception; AgentFrameworkRuntime is a real instance over an
/// empty IConfiguration, which its own fail-fast guard turns into a deterministic
/// InvalidOperationException before any network call. Reaching one or the other proves routing.
/// </summary>
public class AgentRuntimeSelectorTests
{
    private sealed class FakeAiService : IAIService
    {
        public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default) =>
            Task.FromResult("sk-reply");
        public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return "sk-chunk";
            await Task.CompletedTask;
        }
        public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? TenantId { get; init; } = Guid.NewGuid();
        public string[] Roles => [];
        public bool IsAuthenticated => true;
        public string? IpAddress => null;
        public string? CorrelationId => null;
    }

    private sealed class FakePolicy : IAgentRuntimePolicyService
    {
        private readonly AgentRuntimeKind _kind;
        public FakePolicy(AgentRuntimeKind kind) => _kind = kind;
        public Task<AgentRuntimeKind> GetRuntimeAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(_kind);
    }

    private static AgentRuntimeSelector CreateSut(AgentRuntimeKind kind, ICurrentUserService? currentUser = null)
    {
        var traceCollector = new ChatTraceCollector();
        var skRuntime = new AgentRuntime(new FakeAiService(), traceCollector);
        // Empty config — no AI:OpenAI:ApiKey — so BuildAgentAsync's own guard throws before any
        // network call, giving a deterministic, offline-safe way to prove this branch was reached.
        var mafRuntime = new AgentFrameworkRuntime(new ConfigurationBuilder().Build(), new MafToolFunctionFactory(
            new NullToolDefinitionRepository(), null!, null!, currentUser ?? new FakeCurrentUserService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<MafToolFunctionFactory>.Instance), traceCollector);
        return new AgentRuntimeSelector(skRuntime, mafRuntime, new FakePolicy(kind), currentUser ?? new FakeCurrentUserService());
    }

    // Only used to satisfy MafToolFunctionFactory's constructor for this test's purposes — the
    // AgentFrameworkRuntime under test always fails at its own config guard, before
    // MafToolFunctionFactory.BuildToolsAsync would ever run.
    private sealed class NullToolDefinitionRepository : R2WAI.Domain.Interfaces.IRepository<R2WAI.Domain.Entities.ToolDefinition>
    {
        public Task<R2WAI.Domain.Entities.ToolDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition?> GetByIdAsync(Guid id, System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, object>> include, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition?> GetByIdAsync(Guid id, string includePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<R2WAI.Domain.Entities.ToolDefinition>> FindAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, string includePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<R2WAI.Domain.Entities.ToolDefinition> AddAsync(R2WAI.Domain.Entities.ToolDefinition entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(R2WAI.Domain.Entities.ToolDefinition entity) => throw new NotImplementedException();
        public void Delete(R2WAI.Domain.Entities.ToolDefinition entity) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<R2WAI.Domain.Entities.ToolDefinition, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task InvokeAsync_PolicySaysSemanticKernel_RoutesToTheSemanticKernelRuntime()
    {
        var sut = CreateSut(AgentRuntimeKind.SemanticKernel);

        var result = await sut.InvokeAsync(new AgentRuntimeRequest("hello"));

        Assert.Equal("sk-reply", result.Text);
    }

    [Fact]
    public async Task InvokeAsync_PolicySaysAgentFramework_RoutesToTheAgentFrameworkRuntime()
    {
        var sut = CreateSut(AgentRuntimeKind.AgentFramework);

        // Not "runs successfully" — no OpenAI credential exists in this environment. The exception
        // type/message is exactly AgentFrameworkRuntime's own config guard, proving this call
        // actually reached that branch rather than silently falling back to SemanticKernel.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InvokeAsync(new AgentRuntimeRequest("hello")));
        Assert.Contains("AI:OpenAI:ApiKey", ex.Message);
    }

    [Fact]
    public async Task InvokeAsync_NoAmbientTenant_DefaultsToSemanticKernel_NeverConsultsPolicy()
    {
        var sut = CreateSut(AgentRuntimeKind.AgentFramework, currentUser: new FakeCurrentUserService { TenantId = null });

        var result = await sut.InvokeAsync(new AgentRuntimeRequest("hello"));

        Assert.Equal("sk-reply", result.Text);
    }

    [Fact]
    public async Task StreamAsync_PolicySaysSemanticKernel_RoutesToTheSemanticKernelRuntime()
    {
        var sut = CreateSut(AgentRuntimeKind.SemanticKernel);

        var chunks = new List<string>();
        await foreach (var chunk in sut.StreamAsync(new AgentRuntimeRequest("hello")))
            chunks.Add(chunk);

        Assert.Equal(["sk-chunk"], chunks);
    }
}
