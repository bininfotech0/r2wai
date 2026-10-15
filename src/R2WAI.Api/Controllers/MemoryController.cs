using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Controllers;

/// <summary>
/// The signed-in user's own memory: what agents may remember about them, and the switches for it.
/// Every endpoint acts on the caller's memories only; there is no admin view of another user's.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/memory")]
public class MemoryController(IUserMemoryService memory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOverview(CancellationToken ct = default) =>
        Ok(await memory.GetOverviewAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveUserMemoryRequest request, CancellationToken ct = default) =>
        Ok(await memory.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveUserMemoryRequest request, CancellationToken ct = default) =>
        Ok(await memory.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default) =>
        Ok(await memory.ApproveAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await memory.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>partition: all, personal, pending, or an agent's id.</summary>
    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] string partition, CancellationToken ct = default) =>
        Ok(new { removed = await memory.ClearAsync(partition, ct) });

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UserMemorySettingsDto settings, CancellationToken ct = default) =>
        Ok(await memory.UpdateSettingsAsync(settings, ct));

    private static readonly JsonSerializerOptions ExportJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct = default)
    {
        var export = await memory.ExportAsync(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, ExportJson);
        return File(bytes, "application/json", $"r2wai-memory-{export.ExportedAt:yyyyMMdd}.json");
    }
}
