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
    DbSet<User> Users { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }
    DbSet<Document> Documents { get; }
    DbSet<KnowledgeBase> KnowledgeBases { get; }
    DbSet<KnowledgeBaseSource> KnowledgeBaseSources { get; }
    DbSet<Chatbot> Chatbots { get; }
    DbSet<Workflow> Workflows { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowStepExecution> WorkflowStepExecutions { get; }
    DbSet<AssistantDefinition> AssistantDefinitions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ModelConfiguration> ModelConfigurations { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Role> Roles { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ToolDefinition> ToolDefinitions { get; }
    DbSet<WorkflowSchedule> WorkflowSchedules { get; }
    DbSet<WebhookEndpoint> WebhookEndpoints { get; }
    DbSet<ApiKey> ApiKeys { get; }
}

public class ApplicationDbContext : DbContext, ITenantDbContext
{
    private static bool HasTenantIdProperty(Type type) =>
        type.GetProperty("TenantId", typeof(Guid)) != null;

    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediator _mediator;
    private readonly ILogger<ApplicationDbContext> _logger;

    public Guid? TenantId => _currentUserService.TenantId;

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
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<KnowledgeBaseSource> KnowledgeBaseSources => Set<KnowledgeBaseSource>();
    public DbSet<Chatbot> Chatbots => Set<Chatbot>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStepExecution> WorkflowStepExecutions => Set<WorkflowStepExecution>();
    public DbSet<AssistantDefinition> AssistantDefinitions => Set<AssistantDefinition>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ModelConfiguration> ModelConfigurations => Set<ModelConfiguration>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ToolDefinition> ToolDefinitions => Set<ToolDefinition>();
    public DbSet<WorkflowSchedule> WorkflowSchedules => Set<WorkflowSchedule>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(Tenant)) continue;

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
        var userId = _currentUserService.UserId;
        var utcNow = _dateTimeService.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity<Guid>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.GetType().GetProperty("CreatedAt")?.SetValue(entry.Entity, utcNow);
                    if (entry.Entity is BaseAuditableEntity<Guid> auditable)
                    {
                        auditable.GetType().GetProperty("CreatedBy")?.SetValue(auditable, userId?.ToString());
                    }
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(BaseEntity<Guid>.CreatedAt)).IsModified = false;
                    entry.Entity.GetType().GetProperty("ModifiedAt")?.SetValue(entry.Entity, utcNow);
                    if (entry.Entity is BaseAuditableEntity<Guid> modAuditable)
                    {
                        modAuditable.GetType().GetProperty("ModifiedBy")?.SetValue(modAuditable, userId?.ToString());
                    }
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
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue))
                    : null,
                NewValues = entry.State != EntityState.Deleted
                    ? JsonSerializer.Serialize(entry.Properties
                        .Where(p => p.IsModified || entry.State == EntityState.Added)
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))
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
                _currentUserService.IpAddress);

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
        public AuditAction Action { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => TenantId == null || EF.Property<Guid>(e, "TenantId") == TenantId);
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity<Guid>
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity<Guid>
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => (TenantId == null || EF.Property<Guid>(e, "TenantId") == TenantId) && !e.IsDeleted);
    }
}
