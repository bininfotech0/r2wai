using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.KnowledgeBases.Commands;
using R2WAI.Application.Features.KnowledgeBases.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class KnowledgeBasesController(IMediator mediator, ILogger<KnowledgeBasesController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgeBaseCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating knowledge base: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? applicationId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetKnowledgeBasesQuery { Page = page, PageSize = pageSize, Search = search, ApplicationId = applicationId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetKnowledgeBaseSourcesQuery { Status = status, Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/application")]
    public async Task<IActionResult> AssignApplication(Guid id, [FromBody] AssignApplicationRequest request, CancellationToken ct = default)
    {
        var command = new AssignKnowledgeBaseApplicationCommand { Id = id, ApplicationId = request.ApplicationId };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    public record AssignApplicationRequest(Guid? ApplicationId);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetKnowledgeBaseByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKnowledgeBaseCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteKnowledgeBaseCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    public record BulkDeleteRequest(Guid[] Ids);

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request, CancellationToken ct = default)
    {
        await mediator.Send(new BulkDeleteKnowledgeBasesCommand { Ids = request.Ids }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sources")]
    public async Task<IActionResult> AddSource(Guid id, [FromBody] AddSourceCommand command, CancellationToken ct = default)
    {
        command = command with { KnowledgeBaseId = id };
        var result = await mediator.Send(command, ct);
        return Created($"/api/v1/knowledgebases/{id}/sources/{result.Id}", result);
    }

    [HttpDelete("sources/{sourceId:guid}")]
    public async Task<IActionResult> RemoveSource(Guid sourceId, CancellationToken ct = default)
    {
        var command = new RemoveSourceCommand { SourceId = sourceId };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/search")]
    public async Task<IActionResult> Search(Guid id, [FromBody] SearchKnowledgeBaseQuery query, CancellationToken ct = default)
    {
        query = query with { KnowledgeBaseId = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reindex")]
    public async Task<IActionResult> Reindex(Guid id, CancellationToken ct = default)
    {
        var knowledgeBaseService = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.IKnowledgeBaseService>();
        var documentService = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.IDocumentService>();

        logger.LogInformation("Re-indexing knowledge base {KbId}", id);

        var kb = await mediator.Send(new GetKnowledgeBaseByIdQuery { Id = id }, ct);

        var documents = await mediator.Send(new R2WAI.Application.Features.Documents.Queries.GetDocumentsQuery
        {
            KnowledgeBaseId = id,
            Page = 1,
            PageSize = 1000
        }, ct);

        var reindexed = 0;
        foreach (var doc in documents.Items)
        {
            try
            {
                await mediator.Send(new R2WAI.Application.Features.Documents.Commands.ProcessDocumentCommand { DocumentId = doc.Id }, ct);
                reindexed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to re-index document {DocId}", doc.Id);
            }
        }

        return Ok(new { knowledgeBaseId = id, totalDocuments = documents.Items.Count, reindexed, status = "complete" });
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetKnowledgeBaseVersionsQuery { KnowledgeBaseId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(Guid id, [FromBody] CreateKnowledgeBaseVersionCommand command, CancellationToken ct = default)
    {
        command = command with { KnowledgeBaseId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/rollback")]
    public async Task<IActionResult> RollbackVersion(Guid id, Guid versionId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new RollbackKnowledgeBaseVersionCommand { VersionId = versionId }, ct);
        return Ok(result);
    }
}
