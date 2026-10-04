using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.AI.Policies;
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
    private readonly IKnowledgePolicyService _knowledgePolicyService;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        ApplicationDbContext context,
        IAIService aiService,
        IVectorStoreService vectorStore,
        IHttpClientFactory httpClientFactory,
        IKnowledgePolicyService knowledgePolicyService,
        ILogger<KnowledgeBaseService> logger)
    {
        _context = context;
        _aiService = aiService;
        _vectorStore = vectorStore;
        _httpClientFactory = httpClientFactory;
        _knowledgePolicyService = knowledgePolicyService;
        _logger = logger;
    }

    public async Task<KnowledgeBaseDto> CreateKnowledgeBaseAsync(Guid tenantId, Guid userId, string name, string? description, string dataClassification = "Internal", CancellationToken ct = default)
    {
        var knowledgeBase = new KnowledgeBase(Guid.NewGuid(), tenantId, userId, name, description);
        knowledgeBase.SetDataClassification(Enum.Parse<Domain.Enums.DataClassification>(dataClassification, true));
        var collectionName = $"kb_{knowledgeBase.Id:N}";
        knowledgeBase.ConfigureEmbedding("text-embedding-3-small", 1000, 200, collectionName);

        await _context.KnowledgeBases.AddAsync(knowledgeBase, ct);
        await _context.SaveChangesAsync(ct);

        await _vectorStore.CreateCollectionAsync(collectionName, ct: ct);
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

                    // Re-indexing a source replaces its vectors instead of appending a second copy —
                    // see DeterministicChunkId. Without this the old chunk count has to go first,
                    // because a source that got shorter leaves stale high-index chunks behind that
                    // the upsert can no longer overwrite.
                    await DeleteSourceVectorsQuietlyAsync(kb.VectorCollectionName, source.Id, indexingCts.Token);

                    var embeddings = await _aiService.GenerateEmbeddingsAsync(chunks, indexingCts.Token);
                    var vectors = new List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)>();

                    for (var i = 0; i < chunks.Count; i++)
                    {
                        var embedding = embeddings.ElementAtOrDefault(i);
                        if (embedding is null || embedding.Count == 0) continue;

                        vectors.Add((
                            DeterministicChunkId.ForSourceChunk(source.Id, i),
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
            ChunkCount = source.ChunkCount,
            IndexedAt = source.IndexedAt,
            Error = source.Error,
            CreatedAt = source.CreatedAt
        };
    }

    private async Task<string> FetchUrlTextAsync(string url, CancellationToken ct)
    {
        // EgressGuard: this is a server-side fetch of a tenant-supplied URL, so without the guard a
        // tenant admin could point a knowledge source at the cloud metadata endpoint
        // (169.254.169.254) or an internal service and read the response straight back as indexed
        // text. Every other outbound-fetch path in the codebase already goes through this
        // (DynamicToolExecutor, McpClientAdapter, OpenApiImportService, IntegrationsController) —
        // this one was the remaining gap, and the guard is only meaningful at the real dispatch
        // point, not on some separate "test" button.
        if (!Security.EgressGuard.IsAllowedUrl(url))
            throw new InvalidOperationException(
                "URL is not an allowed ingestion target (private, link-local, or non-HTTP address).");

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
            await DeleteSourceVectorsQuietlyAsync(kb.VectorCollectionName, id, ct);
        }

        _context.KnowledgeBaseSources.Remove(source);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Purges a source's vectors, logging rather than throwing on failure. Used on the paths where a
    /// vector-store outage must not block the caller's own bookkeeping — but note this is exactly the
    /// swallow-everything shape that let orphaned vectors accumulate unnoticed for so long, so it is
    /// deliberately confined to one place where it is at least logged at Warning with the source id.
    /// </summary>
    private async Task DeleteSourceVectorsQuietlyAsync(string collectionName, Guid sourceId, CancellationToken ct)
    {
        try
        {
            await _vectorStore.DeleteVectorsBySourceAsync(collectionName, sourceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove vectors for source {SourceId} from {Collection}", sourceId, collectionName);
        }
    }

    public async Task<PagedResult<SearchResultDto>> SearchKnowledgeBaseAsync(Guid knowledgeBaseId, string query, int page, int pageSize, CancellationToken ct = default, Guid? expectedTenantId = null)
    {
        // IgnoreQueryFilters only when the caller supplied expectedTenantId — an [AllowAnonymous]
        // caller with no ambient tenant_id claim (see ChatbotsController) would otherwise always get
        // NotFound here under the fail-closed tenant filter (P0-5), silently killing RAG grounding
        // for every knowledge-base-linked public chatbot. The explicit equality check below is what
        // keeps this safe: it's not a bypass, it's substituting a caller-verified tenant match for
        // the ambient-claim-based one. Every other (authenticated) caller passes null and gets
        // byte-identical ambient-filtered behavior — do not default this to IgnoreQueryFilters.
        var kbQuery = expectedTenantId.HasValue
            ? _context.KnowledgeBases.IgnoreQueryFilters().Where(k => k.TenantId == expectedTenantId.Value)
            : _context.KnowledgeBases;
        var kb = await kbQuery.FirstOrDefaultAsync(k => k.Id == knowledgeBaseId, ct);

        if (kb is null)
            throw new NotFoundException(nameof(KnowledgeBase), knowledgeBaseId);

        // Policy Engine: an optional, tenant-configured data-classification ceiling on what a RAG
        // search may pull into an AI prompt — additive tightening, a tenant with no "Knowledge" policy
        // configured sees byte-identical behavior. Enforced once here rather than at each of the chat
        // entry points that call this method, so no caller can accidentally bypass it.
        var maxClassification = await _knowledgePolicyService.GetMaxClassificationAsync(kb.TenantId, ct);
        if (KnowledgePolicyEvaluator.ExceedsCeiling(kb.DataClassification.ToString(), maxClassification))
        {
            _logger.LogWarning(
                "Blocked RAG search of KB {KbId} — its {Classification} classification exceeds tenant {TenantId}'s configured Knowledge policy ceiling of {Ceiling}",
                knowledgeBaseId, kb.DataClassification, kb.TenantId, maxClassification);
            await _context.AuditLogs.AddAsync(new AuditLog(Guid.NewGuid(), kb.TenantId, AuditAction.Execute, "GlobalPolicy",
                "Knowledge", userId: null, metadata: JsonSerializer.Serialize(new
                {
                    status = "blocked",
                    reason = "knowledge base classification exceeds policy ceiling",
                    knowledgeBaseId,
                    classification = kb.DataClassification.ToString(),
                    ceiling = maxClassification
                })), ct);
            await _context.SaveChangesAsync(ct);

            return new PagedResult<SearchResultDto> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
        }

        var results = new List<SearchResultDto>();

        if (!string.IsNullOrEmpty(kb.VectorCollectionName))
        {
            try
            {
                // ApplicationConfiguration.RagThreshold is the tenant's per-application "RAG relevance
                // threshold" control, surfaced in the Studio UI. It was persisted, validated
                // (InclusiveBetween(0,1)), snapshotted per version and rolled back — and never read
                // by any query, so the control users configured had provably zero effect. Resolved
                // here, from the knowledge base's own linked application, so all nine callers get the
                // behaviour they were always promised. Defaults to 0.7 (no floor) when the KB is not
                // attached to an application or the application has no config row yet, which is the
                // same value the entity seeds.
                var minRelevance = await ResolveRagThresholdAsync(kb, ct);

                var queryVector = await _aiService.GenerateEmbeddingAsync(query, ct);
                var searchResults = await _vectorStore.HybridSearchAsync(
                    kb.VectorCollectionName, [.. queryVector], query, pageSize, 0.7f, minRelevance, ct);

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

    private async Task<float> ResolveRagThresholdAsync(KnowledgeBase kb, CancellationToken ct)
    {
        if (kb.ApplicationId is not { } applicationId)
            return DefaultRagThreshold;

        var configured = await _context.ApplicationConfigurations
            .Where(c => c.ApplicationId == applicationId)
            .Select(c => (double?)c.RagThreshold)
            .FirstOrDefaultAsync(ct);

        return (float)(configured ?? DefaultRagThreshold);
    }

    private static readonly float DefaultRagThreshold = 0.7f;

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
        DataClassification = kb.DataClassification.ToString(),
        CreatedAt = kb.CreatedAt,
        Sources = kb.Sources?.Select(s => new KnowledgeBaseSourceDto
        {
            Id = s.Id,
            Type = s.Type,
            ReferenceId = s.ReferenceId,
            Url = s.Url,
            Content = s.Content?.Length > 200 ? s.Content[..200] + "..." : s.Content,
            Status = s.Status,
            ChunkCount = s.ChunkCount,
            IndexedAt = s.IndexedAt,
            Error = s.Error,
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
