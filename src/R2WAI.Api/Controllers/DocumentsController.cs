using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Features.Documents.Commands;
using R2WAI.Application.Features.Documents.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Policy = "CanManageDocuments")]
[Route("api/v1/[controller]")]
public class DocumentsController(IMediator mediator, ILogger<DocumentsController> logger) : ControllerBase
{
    private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "text/plain", "text/csv",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "text/markdown", "application/json",
    };

    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] Guid? knowledgeBaseId, [FromForm] string? description, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { error = $"File size exceeds the {MaxFileSize / (1024 * 1024)} MB limit." });

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"File type '{file.ContentType}' is not supported." });

        var safeFileName = Path.GetFileName(file.FileName);
        if (safeFileName != file.FileName || safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest(new { error = "Invalid file name." });

        logger.LogInformation("Uploading document: {FileName}, size: {Size}", safeFileName, file.Length);

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }

            var command = new UploadDocumentCommand
            {
                Name = file.FileName,
                FilePath = tempPath,
                FileSize = file.Length,
                FileType = GetDocumentType(file.ContentType),
                KnowledgeBaseId = knowledgeBaseId,
                Description = description
            };

            var result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                try { System.IO.File.Delete(tempPath); } catch { }
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? knowledgeBaseId = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetDocumentsQuery { Page = page, PageSize = pageSize, KnowledgeBaseId = knowledgeBaseId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetDocumentByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken ct = default)
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        var storageService = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.IStorageService>();
        var dbContext = HttpContext.RequestServices.GetRequiredService<R2WAI.Infrastructure.Persistence.ApplicationDbContext>();

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);
        if (document is null)
            return NotFound(new { error = "Document not found." });

        try
        {
            var stream = await storageService.DownloadFileAsync(document.FilePath, ct);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(ct);
            return Ok(new { content });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "Document content not found in storage." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve content for document {DocumentId}", id);
            return StatusCode(500, new { error = "Failed to retrieve document content." });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteDocumentCommand { DocumentId = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id, CancellationToken ct = default)
    {
        var command = new ProcessDocumentCommand { DocumentId = id };
        await mediator.Send(command, ct);
        return Accepted(new { documentId = id, status = "processing" });
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
    {
        var query = new GetDocumentByIdQuery { Id = id };
        var document = await mediator.Send(query, ct);
        return Ok(new { documentId = document.Id, fileName = document.Name, downloadUrl = $"/api/v1/documents/{id}/file" });
    }

    [HttpPost("{id:guid}/summarize")]
    public async Task<IActionResult> Summarize(Guid id, CancellationToken ct = default)
    {
        var command = new SummarizeDocumentCommand { DocumentId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/extract")]
    public async Task<IActionResult> Extract(Guid id, [FromBody] ExtractDocumentCommand command, CancellationToken ct = default)
    {
        command = command with { DocumentId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("compare")]
    public async Task<IActionResult> Compare([FromBody] CompareDocumentsCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/ask")]
    public async Task<IActionResult> Ask(Guid id, [FromBody] AskDocumentCommand command, CancellationToken ct = default)
    {
        command = command with { DocumentId = id };
        var result = await mediator.Send(command, ct);
        return Ok(new { answer = result });
    }

    [HttpPost("bulk-upload")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<IActionResult> BulkUpload(List<IFormFile> files, [FromForm] Guid? knowledgeBaseId, CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { error = "At least one file is required." });

        if (files.Count > 20)
            return BadRequest(new { error = "Maximum 20 files per bulk upload." });

        foreach (var f in files)
        {
            if (f.Length > MaxFileSize)
                return BadRequest(new { error = $"File '{Path.GetFileName(f.FileName)}' exceeds the {MaxFileSize / (1024 * 1024)} MB limit." });
            if (!AllowedContentTypes.Contains(f.ContentType))
                return BadRequest(new { error = $"File type '{f.ContentType}' for '{Path.GetFileName(f.FileName)}' is not supported." });
        }

        logger.LogInformation("Bulk uploading {Count} documents", files.Count);

        var results = new List<object>();
        var errors = new List<object>();

        foreach (var file in files)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                using (var stream = new FileStream(tempPath, FileMode.Create))
                    await file.CopyToAsync(stream, ct);

                var command = new UploadDocumentCommand
                {
                    Name = file.FileName,
                    FilePath = tempPath,
                    FileSize = file.Length,
                    FileType = GetDocumentType(file.ContentType),
                    KnowledgeBaseId = knowledgeBaseId
                };

                var result = await mediator.Send(command, ct);
                results.Add(new { result.Id, result.Name, Status = "Uploaded" });
            }
            catch (Exception ex)
            {
                errors.Add(new { FileName = file.FileName, Error = ex.Message });
            }
        }

        return Ok(new { Uploaded = results, Errors = errors, Total = files.Count, SuccessCount = results.Count, ErrorCount = errors.Count });
    }

    private static Domain.Enums.DocumentType GetDocumentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => Domain.Enums.DocumentType.PDF,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => Domain.Enums.DocumentType.DOCX,
            "application/msword" => Domain.Enums.DocumentType.Text,
            "text/plain" => Domain.Enums.DocumentType.Text,
            "text/csv" => Domain.Enums.DocumentType.Text,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => Domain.Enums.DocumentType.XLSX,
            _ => Domain.Enums.DocumentType.Text
        };
    }
}
