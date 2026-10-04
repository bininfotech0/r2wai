using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Polly;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.Plugins;
using R2WAI.Infrastructure.Authentication;
using R2WAI.Infrastructure.Cache;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Persistence.Repositories;
using R2WAI.Infrastructure.Services;
using R2WAI.Infrastructure.Services.ToolFramework;
using R2WAI.Infrastructure.SignalR;
using R2WAI.Infrastructure.Storage;
using R2WAI.Infrastructure.VectorStore;

namespace R2WAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["Database:ConnectionString"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(3);
                });
            }

            // EF Core 9+ strictness - ignore pending model changes warning for manual migration fixes
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<R2WAI.Application.Common.Interfaces.IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IAadhaarHasher, AadhaarHasher>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IChatbotService, ChatbotService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IApprovalService, ApprovalService>();

        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddHttpToolClient();
        services.AddTransient<ITool, HttpTool>(sp =>
        {
            var options = new HttpToolOptions
            {
                BaseUrl = configuration["Tools:Http:BaseUrl"] ?? "http://localhost",
                ApiKey = configuration["Tools:Http:ApiKey"]
            };
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new HttpTool(httpClientFactory.CreateClient(HttpToolClient.Name), options, sp.GetRequiredService<ILogger<HttpTool>>());
        });
        services.AddTransient<ITool, EmailTool>(sp =>
        {
            var options = new EmailToolOptions
            {
                SmtpHost = configuration["Tools:Email:SmtpHost"] ?? "localhost",
                SmtpPort = int.Parse(configuration["Tools:Email:SmtpPort"] ?? "587"),
                SmtpUser = configuration["Tools:Email:SmtpUser"],
                SmtpPassword = configuration["Tools:Email:SmtpPassword"],
                FromAddress = configuration["Tools:Email:FromAddress"],
                EnableSsl = bool.Parse(configuration["Tools:Email:EnableSsl"] ?? "true")
            };
            return new EmailTool(options, sp.GetRequiredService<ILogger<EmailTool>>());
        });
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<FileProcessingService>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        services.AddScoped<IBackgroundJobQueue, Services.BackgroundJobs.BackgroundJobQueue>();
        services.AddScoped<IBackgroundJobHandler, Services.BackgroundJobs.NotifyApproversJobHandler>();
        services.AddScoped<IBackgroundJobHandler, Services.BackgroundJobs.IndexDocumentJobHandler>();
        services.AddSingleton<IRequestMetricsStore, RequestMetricsStore>();
        services.AddHostedService<Services.BackgroundJobs.BackgroundJobProcessor>();

        services.AddSingleton<AI.ModelGateway.IModelProvider, AI.ModelGateway.OpenAiModelProvider>();
        services.AddSingleton<AI.ModelGateway.IModelProvider, AI.ModelGateway.OllamaModelProvider>();
        services.AddSingleton<AI.ModelGateway.IModelProvider, AI.ModelGateway.ZaiModelProvider>();
        services.AddSingleton<AI.ModelGateway.IModelGateway, AI.ModelGateway.ModelGateway>();
        services.AddScoped<IModelConfigurationResolver, AI.ModelGateway.ModelConfigurationResolver>();
        services.AddScoped<IAIService, SemanticKernelService>();
        services.AddScoped<DocumentPlugin>();
        services.AddScoped<RAGPlugin>();
        services.AddScoped<WorkflowPlugin>();
        services.AddScoped<AssistantPlugin>();
        services.AddScoped<IChatTraceCollector, ChatTraceCollector>();
        services.AddScoped<IChatStreamContext, AI.ChatStreamContext>();
        services.AddScoped<IEnabledToolScope, AI.EnabledToolScope>();
        services.AddScoped<IToolGateway, AI.ToolGateway>();
        services.AddScoped<AiFunctionAuditFilter>();
        services.AddScoped<IToolExecutionPolicyService, ToolExecutionPolicyService>();
        services.AddScoped<IAiUsagePolicyService, AI.Policies.AiUsagePolicyService>();
        services.AddScoped<IPiiPolicyService, AI.Policies.PiiPolicyService>();
        services.AddScoped<IKnowledgePolicyService, AI.Policies.KnowledgePolicyService>();
        services.AddScoped<IApprovalPolicyService, AI.Policies.ApprovalPolicyService>();
        services.AddScoped<IAuthPolicyService, Authentication.AuthPolicyService>();
        services.AddScoped<IDataRetentionService, AI.Policies.DataRetentionService>();
        services.AddScoped<IPromptTemplateService, AI.Prompts.PromptTemplateService>();
        services.AddScoped<IWorkflowTemplateService, Workflows.WorkflowTemplateService>();
        services.AddScoped<IConversationMemoryService, AI.ConversationMemoryService>();
        services.AddScoped<AI.AgentRuntime>();
        services.AddScoped<AI.AgentFrameworkRuntime>();
        services.AddScoped<AI.DynamicTools.MafToolFunctionFactory>();
        services.AddScoped<IAgentRuntimePolicyService, AI.AgentRuntimePolicyService>();
        services.AddScoped<IAgentRuntime, AI.AgentRuntimeSelector>();
        services.AddScoped<IAgenticRetrievalOrchestrator, AI.AgenticRetrievalOrchestrator>();
        services.AddScoped<AI.DynamicTools.DynamicToolExecutor>();
        services.AddScoped<AI.DynamicTools.McpClientAdapter>();
        services.AddScoped<AI.DynamicTools.McpDynamicToolExecutor>();
        services.AddScoped<AI.DynamicTools.DynamicToolFunctionFactory>();
        services.AddScoped<IDeferredToolCallExecutor, AI.DynamicTools.DeferredToolCallExecutor>();

        services.AddScoped<IVectorStoreService, PgVectorService>();
        services.AddScoped<IOpenApiImportService, Integrations.OpenApiImportService>();

        var storageMode = configuration["Storage:Mode"]
            ?? configuration["Storage:Provider"]
            ?? "local";
        if (storageMode.Equals("minio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageService, MinioStorageService>();
        }
        else
        {
            services.AddSingleton<IStorageService, LocalStorageService>();
        }

        var redisConnection = configuration["Cache:Redis:ConnectionString"]
            ?? configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        }

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<JwtService>();
        services.AddSingleton<TotpService>();
        // Singleton so its cached OpenIdConnectConfiguration survives across requests — see the
        // class's own doc comment for why (an [AllowAnonymous] endpoint's discovery-fetch cache).
        services.AddSingleton<EntraIdAuthService>();

        services.AddSignalR();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddHttpContextAccessor();

        return services;
    }
}
