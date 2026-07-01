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

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IChatbotService, ChatbotService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IApprovalService, ApprovalService>();

        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddHttpClient("HttpTool", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "R2WAI-ToolFramework/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))))
        .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
        services.AddTransient<ITool, HttpTool>(sp =>
        {
            var options = new HttpToolOptions
            {
                BaseUrl = configuration["Tools:Http:BaseUrl"] ?? "http://localhost",
                ApiKey = configuration["Tools:Http:ApiKey"]
            };
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new HttpTool(httpClientFactory.CreateClient("HttpTool"), options, sp.GetRequiredService<ILogger<HttpTool>>());
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

        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<BackgroundTaskProcessor>();

        services.AddScoped<IAIService, SemanticKernelService>();
        services.AddScoped<DocumentPlugin>();
        services.AddScoped<RAGPlugin>();
        services.AddScoped<WorkflowPlugin>();
        services.AddScoped<AssistantPlugin>();

        services.AddScoped<IVectorStoreService, PgVectorService>();

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
        services.AddScoped<EntraIdAuthService>();
        services.AddScoped<AuthorizationService>();

        services.AddSignalR();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddHttpContextAccessor();

        return services;
    }
}
