using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record UploadDocumentCommand : IRequest<DocumentDto>
{
    public string Name { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DocumentType FileType { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public string? Description { get; init; }
}

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(500).WithMessage("File name must not exceed 500 characters.");
        RuleFor(v => v.FilePath)
            .NotEmpty().WithMessage("File path is required.");
        RuleFor(v => v.FileSize)
            .GreaterThan(0).WithMessage("File size must be greater than 0.")
            .LessThanOrEqualTo(100_000_000).WithMessage("File size must not exceed 100MB.");
    }
}

public class UploadDocumentCommandHandler(
    IRepository<Document> documentRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IStorageService storageService,
    IMapper mapper,
    ILogger<UploadDocumentCommandHandler> logger) : IRequestHandler<UploadDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        string storagePath;
        try
        {
            await using var fileStream = new FileStream(command.FilePath, FileMode.Open, FileAccess.Read);
            var contentType = GetContentType(command.Name);
            storagePath = await storageService.UploadFileAsync(
                fileStream, command.Name, contentType, $"tenants/{tenantId}/documents", cancellationToken);
            
            // Clean up temp file
            try { File.Delete(command.FilePath); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload document {Name} to storage", command.Name);
            throw new Exception("Failed to upload document to storage.", ex);
        }

        var document = new Document(
            Guid.NewGuid(), tenantId, userId, command.Name,
            command.FileType, storagePath, command.FileSize,
            command.KnowledgeBaseId, command.Description);

        await documentRepo.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<DocumentDto>(document);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }
}
