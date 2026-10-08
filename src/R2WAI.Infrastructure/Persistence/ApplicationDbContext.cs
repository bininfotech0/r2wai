using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace R2WAI.Infrastructure.Persistence;

public interface ITenantDbContext
{
    Guid? TenantId { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<Department> Departments { get; }
    DbSet<ConnectedApplication> Applications { get; }
    DbSet<ApplicationApi> ApplicationApis { get; }
    DbSet<ApplicationConfiguration> ApplicationConfigurations { get; }
    DbSet<ApplicationVersion> ApplicationVersions { get; }
    DbSet<NavigationDefinition> NavigationDefinitions { get; }
    DbSet<GlobalPolicy> GlobalPolicies { get; }
    DbSet<User> Users { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }
    DbSet<Document> Documents { get; }
    DbSet<KnowledgeBase> KnowledgeBases { get; }
    DbSet<KnowledgeBaseSource> KnowledgeBaseSources { get; }
    DbSet<Chatbot> Chatbots { get; }
    DbSet<Workflow> Workflows { get; }
    DbSet<WorkflowVersion> WorkflowVersions { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowStepExecution> WorkflowStepExecutions { get; }
    DbSet<AssistantDefinition> AssistantDefinitions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ModelConfiguration> ModelConfigurations { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Role> Roles { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ApprovalNotificationDispatch> ApprovalNotificationDispatches { get; }
    DbSet<ToolExecution> ToolExecutions { get; }
    DbSet<ToolDefinition> ToolDefinitions { get; }
    DbSet<ToolDefinitionVersion> ToolDefinitionVersions { get; }
    DbSet<BusinessCapability> BusinessCapabilities { get; }
    DbSet<KnowledgeBaseVersion> KnowledgeBaseVersions { get; }
    DbSet<PromptTemplate> PromptTemplates { get; }
    DbSet<AssistantPromptHistory> AssistantPromptHistories { get; }
    DbSet<WebhookEndpoint> WebhookEndpoints { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<ChatbotChannel> ChatbotChannels { get; }
    DbSet<ChatbotSessionTurn> ChatbotSessionTurns { get; }
    DbSet<TestCase> TestCases { get; }
    DbSet<TestRun> TestRuns { get; }
    DbSet<TestCaseResult> TestCaseResults { get; }
    DbSet<AccessRequest> AccessRequests { get; }
    DbSet<WorkflowTemplateOverride> WorkflowTemplateOverrides { get; }
    DbSet<McpServerConnection> McpServerConnections { get; }
}

public class ApplicationDbContext : DbContext, ITenantDbContext, R2WAI.Application.Common.Interfaces.IApplicationDbContext
{
    private static bool HasTenantIdProperty(Type type) =>
        type.GetProperty("TenantId", typeof(Guid)) != null;

    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediator _mediator;
    private readonly ILogger<ApplicationDbContext> _logger;

    public Guid? TenantId => _currentUserService.TenantId;

    public IQueryable<T> Query<T>() where T : class => Set<T>();

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService,
        IMediator mediator,
        ILogger<ApplicationDbContext> logger)
        : base(options)
    {
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
        _mediator = mediator;
        _logger = logger;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ConnectedApplication> Applications => Set<ConnectedApplication>();
    public DbSet<ApplicationApi> ApplicationApis => Set<ApplicationApi>();
    public DbSet<ApplicationConfiguration> ApplicationConfigurations => Set<ApplicationConfiguration>();
    public DbSet<ApplicationVersion> ApplicationVersions => Set<ApplicationVersion>();
    public DbSet<NavigationDefinition> NavigationDefinitions => Set<NavigationDefinition>();
    public DbSet<GlobalPolicy> GlobalPolicies => Set<GlobalPolicy>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<KnowledgeBaseSource> KnowledgeBaseSources => Set<KnowledgeBaseSource>();
    public DbSet<Chatbot> Chatbots => Set<Chatbot>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStepExecution> WorkflowStepExecutions => Set<WorkflowStepExecution>();
    public DbSet<AssistantDefinition> AssistantDefinitions => Set<AssistantDefinition>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ModelConfiguration> ModelConfigurations => Set<ModelConfiguration>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalNotificationDispatch> ApprovalNotificationDispatches => Set<ApprovalNotificationDispatch>();
    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();
    public DbSet<ToolDefinition> ToolDefinitions => Set<ToolDefinition>();
    public DbSet<ToolDefinitionVersion> ToolDefinitionVersions => Set<ToolDefinitionVersion>();
    public DbSet<BusinessCapability> BusinessCapabilities => Set<BusinessCapability>();
    public DbSet<KnowledgeBaseVersion> KnowledgeBaseVersions => Set<KnowledgeBaseVersion>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<AssistantPromptHistory> AssistantPromptHistories => Set<AssistantPromptHistory>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ChatbotChannel> ChatbotChannels => Set<ChatbotChannel>();
    public DbSet<ChatbotSessionTurn> ChatbotSessionTurns => Set<ChatbotSessionTurn>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<TestCaseResult> TestCaseResults => Set<TestCaseResult>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<WorkflowTemplateOverride> WorkflowTemplateOverrides => Set<WorkflowTemplateOverride>();
    public DbSet<McpServerConnection> McpServerConnections => Set<McpServerConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Tenant used to be hard-skipped here entirely (avoiding a circular self-filter), but
            // that also silently skipped the one filter it DOES legitimately need: soft-delete.
            // HasTenantIdProperty already returns false for Tenant (it has Id, not TenantId — it IS
            // the tenant), so the branches below naturally never apply a tenant filter to it; no
            // special case is actually needed for that part. Found live: DeleteTenantCommand
            // (2026-09-29) was the first thing in the whole codebase to ever call Tenant.SoftDelete(),
            // and a "deleted" tenant kept appearing in GetTenantsQuery's results because nothing was
            // filtering IsDeleted on Tenant queries at all.
            var hasTenant = HasTenantIdProperty(entityType.ClrType);
            var hasSoftDelete = typeof(BaseEntity<Guid>).IsAssignableFrom(entityType.ClrType);

            if (hasTenant && hasSoftDelete)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyTenantAndSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(this, [modelBuilder]);
            }
            else if (hasTenant)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(this, [modelBuilder]);
            }
            else if (hasSoftDelete)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(this, [modelBuilder]);
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeService.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity<Guid>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // A Message's own construction time is meaningful: the user turn and the reply are
                    // usually saved in one SaveChanges after the model call, so stamping both with the
                    // save time collapsed every response time to 0s and made the pair's order ambiguous.
                    // Same for a chatbot session turn: the visitor turn and the reply share one save.
                    if (entry.Entity is Message or ChatbotSessionTurn) break;
                    entry.Entity.GetType().GetProperty("CreatedAt")?.SetValue(entry.Entity, utcNow);
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(BaseEntity<Guid>.CreatedAt)).IsModified = false;
                    entry.Entity.GetType().GetProperty("ModifiedAt")?.SetValue(entry.Entity, utcNow);
                    break;
            }
        }

        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity<Guid>>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        var auditEntries = OnBeforeSaveAudit();

        OnAfterSaveAudit(auditEntries);

        var result = await base.SaveChangesAsync(cancellationToken);

        await DispatchDomainEventsAsync(domainEvents, cancellationToken);

        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        return result;
    }

    private static readonly HashSet<string> SensitiveAuditFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "RefreshTokenHash", "PasswordResetToken", "MfaSecret",
        "AadhaarNumberEncrypted", "AadhaarNumberHash",
        "ApiKeyEncrypted", "SecretHash", "ClientSecret", "EncryptionKey"
    };

    private List<AuditEntry> OnBeforeSaveAudit()
    {
        var entries = new List<AuditEntry>();
        foreach (var entry in ChangeTracker.Entries<BaseEntity<Guid>>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id.ToString(),
                ApplicationId = entry.Entity.GetType().GetProperty("ApplicationId")?.GetValue(entry.Entity) as Guid?,
                Action = entry.State switch
                {
                    EntityState.Added => AuditAction.Create,
                    EntityState.Deleted => AuditAction.Delete,
                    EntityState.Modified => AuditAction.Update,
                    _ => AuditAction.View
                },
                OldValues = entry.State == EntityState.Modified
                    ? JsonSerializer.Serialize(entry.Properties
                        .Where(p => p.IsModified && !p.Metadata.IsKey())
                        .ToDictionary(p => p.Metadata.Name,
                            p => SensitiveAuditFields.Contains(p.Metadata.Name) ? "***REDACTED***" : p.OriginalValue))
                    : null,
                NewValues = entry.State != EntityState.Deleted
                    ? JsonSerializer.Serialize(entry.Properties
                        .Where(p => p.IsModified || entry.State == EntityState.Added)
                        .ToDictionary(p => p.Metadata.Name,
                            p => SensitiveAuditFields.Contains(p.Metadata.Name) ? "***REDACTED***" : p.CurrentValue))
                    : null
            };
            entries.Add(auditEntry);
        }
        return entries;
    }

    private void OnAfterSaveAudit(List<AuditEntry> auditEntries)
    {
        foreach (var auditEntry in auditEntries)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId is null) continue;

            var auditLog = new AuditLog(
                Guid.NewGuid(),
                tenantId.Value,
                auditEntry.Action,
                auditEntry.EntityType,
                auditEntry.EntityId,
                _currentUserService.UserId,
                auditEntry.OldValues,
                auditEntry.NewValues,
                _currentUserService.IpAddress,
                userAgent: null,
                metadata: null,
                applicationId: auditEntry.ApplicationId,
                correlationId: _currentUserService.CorrelationId);

            AuditLogs.Add(auditLog);
        }
    }

    private async Task DispatchDomainEventsAsync(List<BaseDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }

    private sealed class AuditEntry
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public Guid? ApplicationId { get; set; }
        public AuditAction Action { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
    }

    // P0-5 (2026-09-20 audit): this used to be `TenantId == null || EF.Property<Guid>(e, "TenantId")
    // == TenantId` — fail-*open*. When the ambient tenant (ICurrentUserService.TenantId, from the
    // JWT's tenant_id claim) is null, that matched EVERY tenant's rows instead of none. Every currently-
    // known caller that legitimately needs a null ambient tenant already bypasses this filter
    // explicitly via .IgnoreQueryFilters() (every background sweeper, P0-3 same session) — nothing
    // relies on the fail-open branch actually firing — so failing closed here removes a live risk for
    // any *future* authenticated-but-tenant-missing path (a bug, a new SSO flow, a service account)
    // without changing behavior for any real, verified-safe caller today. See
    // TenantIsolationFailClosedTests.cs for the regression coverage.
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => TenantId != null && EF.Property<Guid>(e, "TenantId") == TenantId);
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity<Guid>
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity<Guid>
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => TenantId != null && EF.Property<Guid>(e, "TenantId") == TenantId && !e.IsDeleted);
    }
}
