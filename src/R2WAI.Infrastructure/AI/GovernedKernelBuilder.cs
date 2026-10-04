using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Builds a tool-enabled kernel so that governance can never be missing from a kernel that has tools.
///
/// The governance filter used to be attached LAST, after every plugin, inside a try/catch that logged and
/// carried on — so any failure while attaching plugins (a transient database error while loading a
/// tenant's dynamic tools, say) returned a kernel that already had the mutating built-in tools but no
/// governance filter. Now the filter is attached first, and if anything fails the caller gets a kernel
/// with NO tools rather than a partially attached, ungoverned one.
/// </summary>
public static class GovernedKernelBuilder
{
    public static async Task<Kernel> BuildAsync(
        Kernel baseKernel,
        Func<IFunctionInvocationFilter> governanceFilterFactory,
        Func<Kernel, Task> attachTools,
        ILogger logger)
    {
        // Tool-enabled kernels close over this request's scoped services, so they are never shared.
        var kernel = baseKernel.Clone();
        try
        {
            kernel.FunctionInvocationFilters.Add(governanceFilterFactory());
            await attachTools(kernel);
            return kernel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to build the governed tool kernel; continuing WITHOUT tools rather than with a partial, ungoverned tool set");
            return baseKernel;
        }
    }
}
