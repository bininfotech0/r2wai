using System.Collections.Concurrent;

namespace R2WAI.Infrastructure.Services.ToolFramework;

public interface IToolRegistry
{
    void Register(ITool tool);
    ITool? Get(string name);
    IEnumerable<ITool> GetAll();
}

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry(IEnumerable<ITool>? tools = null)
    {
        if (tools is not null)
        {
            foreach (var tool in tools)
                Register(tool);
        }
    }

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public ITool? Get(string name)
    {
        return _tools.GetValueOrDefault(name);
    }

    public IEnumerable<ITool> GetAll()
    {
        return _tools.Values;
    }
}
