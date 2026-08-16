using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.VectorStore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace R2WAI.Infrastructure.Services;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    // Same containment bound as DocumentService.ProcessDocumentAsync: the embeddings step can run
    // away in resource use independent of chunk count (see D8), and this URL/text source path
    // hits the identical GenerateEmbeddingsAsync call with no bound at all otherwise.
    private static readonly TimeSpan IndexingTimeout = TimeSpan.FromSeconds(90);

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly IVectorStoreService _vectorStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        ApplicationDbContext context,
        IAIService aiService,
        IVectorStoreService vectorStore,
        IHttpClientFactory httpClientFactory,
        ILogger<KnowledgeBaseService> logger)
    {
        _context = context;
        _aiService = aiService;
        _vectorStore = vectorStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<KnowledgeBaseDto> CreateKnowledgeBaseAsync(Guid tenantId, Guid userId, string name, string? description, CancellationToken ct = default)
    {
        var knowledgeBase = new KnowledgeBase(Guid.NewGuid(), tenantId, userId, name, description);
        var collectionName = $"kb_{knowledgeBase.Id:N}";
        knowledgeBase.ConfigureEmbedding("text-embedding-3-small", 1000, 200, collectionName);

        await _context.KnowledgeBases.AddAsync(knowledgeBase, ct);
        await _context.SaveChangesAsync(ct);

        await _vectorStore.CreateCollectionAsync(collectionName, 1536, ct);
        knowledgeBase.UpdateStatus(KnowledgeBaseStatus.Active);
        await _context.SaveChangesAsync(ct);

        return MapToDto(knowledgeBase);
    }

    public async Task<KnowledgeBaseDto> UpdateKnowledgeBaseAsync(Guid id, string name, string? description, CancellationToken ct = default)
    {
        var kb = await _context.KnowledgeBases
            .Include(k => k.Sources)
            .FirstOrDefaultAsync(k => k.Id == id, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), id);

        kb.UpdateDetails(name, description);

        await _context.SaveChangesAsync(ct);

        return MapToDto(kb);
    }

    public async Task DeleteKnowledgeBaseAsync(Guid id, CancellationToken ct = default)
    {
        var kb = await _context.KnowledgeBases
            .FirstOrDefaultAsync(k => k.Id == id, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), id);

        if (!string.IsNullOrEmpty(kb.VectorCollectionName))
        {
            try { await _vectorStore.DeleteCollectionAsync(kb.VectorCollectionName, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete vector collection {Collection}", kb.VectorCollectionName); }
        }

        kb.SoftDelete();
        await _context.SaveChangesAsync(ct);
    }

    public async Task<KnowledgeBaseSourceDto> AddSourceAsync(Guid knowledgeBaseId, string type, Guid? referenceId, string? url, string? content, CancellationToken ct = default)
    {
        var kb = await _context.KnowledgeBases
            .FirstOrDefaultAsync(k => k.Id == knowledgeBaseId, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), knowledgeBaseId);

        var source = new KnowledgeBaseSource(Guid.NewGuid(), knowledgeBaseId, type, referenceId, url, content);
        _context.KnowledgeBaseSources.Add(source);
        await _context.SaveChangesAsync(ct);

        var textToIndex = content;
        if (string.IsNullOrEmpty(textToIndex) && type.Equals("Url", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(url))
        {
            source.MarkProcessing();
            await _context.SaveChangesAsync(ct);

            try { textToIndex = await FetchUrlTextAsync(url, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch URL content for source {SourceId}: {Url}", source.Id, url);
                source.MarkFailed($"Could not fetch URL: {ex.Message}");
                await _context.SaveChangesAsync(ct);
                textToIndex = null;
            }
        }

        if (!string.IsNullOrEmpty(textToIndex) && !string.IsNullOrEmpty(kb.VectorCollectionName))
        {
            source.MarkProcessing();
            await _context.SaveChangesAsync(ct);

            try
            {
                var chunkSize = kb.ChunkSize ?? 1000;
                var chunkOverlap = kb.ChunkOverlap ?? 200;
                var chunks = ChunkText(textToIndex, chunkSize, chunkOverlap);

                if (chunks.Count > 0)
                {
                    using var indexingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    indexingCts.CancelAfter(IndexingTimeout);

                    var embeddings = await _aiService.GenerateEmbeddingsAsync(chunks, indexingCts.Token);
                    var vectors = new List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)>();

                    for (var i = 0; i < chunks.Count; i++)
                    {
                        var embedding = embeddings.ElementAtOrDefault(i);
                        if (embedding is null || embedding.Count == 0) continue;

                        vectors.Add((
                            Guid.NewGuid(),
                            [.. embedding],
                            new Dictionary<string, object>
                            {
                                ["content"] = chunks[i],
                                ["source"] = $"source_{source.Id}",
                                ["sourceId"] = source.Id.ToString(),
                                ["chunkIndex"] = i
                            }
                        ));
                    }

                    if (vectors.Count > 0)
                    {
                        await _vectorStore.UpsertVectorsAsync(kb.VectorCollectionName, vectors, indexingCts.Token);
                        _logger.LogInformation("Indexed {ChunkCount} chunks from source {SourceId} into {Collection}",
                            vectors.Count, source.Id, kb.VectorCollectionName);
                    }

                    source.MarkIndexed(vectors.Count);
                }
                else
                {
                    source.MarkFailed("No content could be extracted to index.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index source {SourceId} into vector store", source.Id);
                source.MarkFailed(ex.Message);
            }

            await _context.SaveChangesAsync(ct);
        }

        return new KnowledgeBaseSourceDto
        {
            Id = source.Id,
            Type = source.Type,
            ReferenceId = source.ReferenceId,
            Url = source.Url,
            Content = source.Content?.Length > 200 ? source.Content[..200] + "..." : source.Content,
            Status = source.Status,
            CreatedAt = source.CreatedAt
        };
    }

    private async Task<string> FetchUrlTextAsync(string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("R2WAI-KnowledgeIndexer/1.0");

        var html = await client.GetStringAsync(url, ct);
        return ExtractReadableText(html);
    }

    private static string ExtractReadableText(string html)
    {
        var withoutScripts = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", " ",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withoutScripts, "<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
        var collapsed = Regex.Replace(decoded, @"\s+", " ").Trim();
        const int maxLength = 200_000;
        return collapsed.Length > maxLength ? collapsed[..maxLength] : collapsed;
    }

    public async Task RemoveSourceAsync(Guid id, CancellationToken ct = default)
    {
        var source = await _context.KnowledgeBaseSources
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (source is null)
            throw new NotFoundException(nameof(KnowledgeBaseSource), id);

        var kb = await _context.KnowledgeBases
            .FirstOrDefaultAsync(k => k.Id == source.KnowledgeBaseId, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), source.KnowledgeBaseId);

        if (!string.IsNullOrEmpty(kb.VectorCollectionName))
        {
            try
            {
                var searchVector = await _aiService.GenerateEmbeddingAsync(source.Content ?? string.Empty, ct);
                var results = await _vectorStore.SearchVectorsAsync(
                    kb.VectorCollectionName, [.. searchVector], 100, ct);

                var matchingPointIds = results
                    .Where(r => r.Payload?.ContainsKey("sourceId") == true &&
                                r.Payload["sourceId"]?.ToString() == id.ToString())
                    .Select(r => r.Id)
                    .ToList();

                if (matchingPointIds.Count > 0)
                {
                    await _vectorStore.DeleteVectorsAsync(kb.VectorCollectionName, matchingPointIds, ct);
                    _logger.LogInformation("Removed {Count} vectors for source {SourceId}", matchingPointIds.Count, id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove vectors for source {SourceId}", id);
            }
        }

        _context.KnowledgeBaseSources.Remove(source);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<SearchResultDto>> SearchKnowledgeBaseAsync(Guid knowledgeBaseId, string query, int page, int pageSize, CancellationToken ct = default)
    {
        var kb = await _context.KnowledgeBases
            .FirstOrDefaultAsync(k => k.Id == knowledgeBaseId, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), knowledgeBaseId);

        var results = new List<SearchResultDto>();

        if (!string.IsNullOrEmpty(kb.VectorCollectionName))
        {
            try
            {
                var queryVector = await _aiService.GenerateEmbeddingAsync(query, ct);
                var searchResults = await _vectorStore.HybridSearchAsync(
                    kb.VectorCollectionName, [.. queryVector], query, pageSize, 0.7f, ct);

                results.AddRange(searchResults.Select(r => new SearchResultDto
                {
                    Id = r.Id,
                    Content = r.Payload?.TryGetValue("content", out var c) == true ? c?.ToString() ?? string.Empty : string.Empty,
                    Score = r.Score,
                    SourceName = r.Payload?.TryGetValue("source", out var s) == true ? s?.ToString() : null,
                    Metadata = JsonSerializer.Serialize(r.Payload)
                }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Vector search failed for KB {KbId}, falling back to text search", knowledgeBaseId);
            }
        }

        var totalCount = results.Count;
        var pagedItems = results
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<SearchResultDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<KnowledgeBaseDto>> GetKnowledgeBasesAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.KnowledgeBases
            .Include(k => k.Sources)
            .Where(k => k.TenantId == tenantId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => MapToDto(k))
            .ToListAsync(ct);

        return new PagedResult<KnowledgeBaseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<KnowledgeBaseDto> GetKnowledgeBaseByIdAsync(Guid id, CancellationToken ct = default)
    {
        var kb = await _context.KnowledgeBases
            .Include(k => k.Sources)
            .FirstOrDefaultAsync(k => k.Id == id, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), id);

        return MapToDto(kb);
    }

    private static KnowledgeBaseDto MapToDto(KnowledgeBase kb) => new()
    {
        Id = kb.Id,
        Name = kb.Name,
        Description = kb.Description,
        Status = kb.Status,
        EmbeddingModel = kb.EmbeddingModel,
        ChunkSize = kb.ChunkSize,
        ChunkOverlap = kb.ChunkOverlap,
        DocumentCount = kb.DocumentCount,
        CreatedAt = kb.CreatedAt,
        Sources = kb.Sources?.Select(s => new KnowledgeBaseSourceDto
        {
            Id = s.Id,
            Type = s.Type,
            ReferenceId = s.ReferenceId,
            Url = s.Url,
            Content = s.Content?.Length > 200 ? s.Content[..200] + "..." : s.Content,
            Status = s.Status,
            CreatedAt = s.CreatedAt
        }).ToList() ?? []
    };

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text)) return chunks;
        if (text.Length <= chunkSize) { chunks.Add(text); return chunks; }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOf(' ', end, chunkSize);
                if (lastSpace > start) end = lastSpace;
            }
            chunks.Add(text[start..end]);
            start = end - overlap;
            if (start >= text.Length) break;
        }
        return chunks;
    }
}
