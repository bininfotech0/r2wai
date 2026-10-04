using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Audit finding P0-4. The governance filter used to be attached last, inside a try/catch that swallowed
/// errors, so a failure while attaching tools returned a kernel that already had mutating tools and no
/// governance filter.
/// </summary>
public class GovernedKernelBuilderTests
{
    private sealed class PassThroughFilter : IFunctionInvocationFilter
    {
        public Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next) => next(context);
    }

    private static Kernel NewBaseKernel() => Kernel.CreateBuilder().Build();

    private static KernelFunction Tool() => KernelFunctionFactory.CreateFromMethod(() => "x", "tool");

    [Fact]
    public async Task The_governance_filter_is_attached_before_any_tool()
    {
        var filtersWhenToolsWereAttached = -1;

        var kernel = await GovernedKernelBuilder.BuildAsync(
            NewBaseKernel(),
            () => new PassThroughFilter(),
            k =>
            {
                filtersWhenToolsWereAttached = k.FunctionInvocationFilters.Count;
                k.Plugins.AddFromFunctions("Tools", [Tool()]);
                return Task.CompletedTask;
            },
            NullLogger.Instance);

        Assert.Equal(1, filtersWhenToolsWereAttached);
        Assert.Single(kernel.Plugins);
        Assert.Single(kernel.FunctionInvocationFilters);
    }

    [Fact]
    public async Task A_failure_while_attaching_tools_yields_a_kernel_with_no_tools_at_all()
    {
        var baseKernel = NewBaseKernel();

        var kernel = await GovernedKernelBuilder.BuildAsync(
            baseKernel,
            () => new PassThroughFilter(),
            k =>
            {
                k.Plugins.AddFromFunctions("Tools", [Tool()]); // attached, then the next step fails
                throw new InvalidOperationException("database unavailable");
            },
            NullLogger.Instance);

        Assert.Same(baseKernel, kernel);
        Assert.Empty(kernel.Plugins);
        Assert.Empty(kernel.FunctionInvocationFilters);
    }

    [Fact]
    public async Task A_failure_creating_the_filter_also_yields_no_tools()
    {
        var baseKernel = NewBaseKernel();
        var attachRan = false;

        var kernel = await GovernedKernelBuilder.BuildAsync(
            baseKernel,
            () => throw new InvalidOperationException("filter unavailable"),
            _ => { attachRan = true; return Task.CompletedTask; },
            NullLogger.Instance);

        Assert.False(attachRan);
        Assert.Same(baseKernel, kernel);
    }

    [Fact]
    public async Task The_shared_base_kernel_is_never_modified_by_a_successful_build()
    {
        var baseKernel = NewBaseKernel();

        await GovernedKernelBuilder.BuildAsync(
            baseKernel,
            () => new PassThroughFilter(),
            k => { k.Plugins.AddFromFunctions("Tools", [Tool()]); return Task.CompletedTask; },
            NullLogger.Instance);

        Assert.Empty(baseKernel.Plugins);
        Assert.Empty(baseKernel.FunctionInvocationFilters);
    }
}
