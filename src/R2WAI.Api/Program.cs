using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using R2WAI.Api.Filters;
using R2WAI.Api.Hubs;
using R2WAI.Api.Middleware;
using R2WAI.Api.HealthChecks;
using R2WAI.Api.Services;
using R2WAI.Api.Workflows;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application;
using R2WAI.Application.Common;
using R2WAI.Infrastructure;
using R2WAI.Infrastructure.Persistence;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using FastEndpoints;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = builder.Environment.IsDevelopment();
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<R2WAI.Api.Logging.SensitiveDataEnricher>()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var elsaConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(elsaConnectionString) && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddElsa(elsa =>
    {
        elsa.UseWorkflowManagement(mgmt =>
        {
            mgmt.UseWorkflowDefinitions(ef => ef.UseEntityFrameworkCore(db => db.UsePostgreSql(elsaConnectionString)));
            mgmt.UseWorkflowInstances(ef => ef.UseEntityFrameworkCore(db => db.UsePostgreSql(elsaConnectionString)));
        });

        elsa.UseWorkflowRuntime(runtime =>
        {
            runtime.UseEntityFrameworkCore(db => db.UsePostgreSql(elsaConnectionString));
        });

        elsa
            .UseHttp()
            .UseScheduling()
            .UseEmail()
            .UseJavaScript()
            .AddActivity<ApprovalStepActivity>()
            .AddActivity<InvokeSemanticKernelActivity>();
    });
}
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddFastEndpoints();
builder.Services.AddSingleton<R2WAI.Api.Hubs.IWorkflowStatusService, R2WAI.Api.Hubs.WorkflowStatusService>();
builder.Services.AddHostedService<R2WAI.Infrastructure.Services.EscalationBackgroundService>();

var jwtSecret = builder.Configuration["Authentication:Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is not configured");
var jwtIssuer = builder.Configuration["Authentication:Jwt:Issuer"] ?? "R2WAI";
var jwtAudience = builder.Configuration["Authentication:Jwt:Audience"] ?? "R2WAI-API";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs")))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SystemAdmin"));

    options.AddPolicy("TenantAccess", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == "tenant_id")));

    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireRole("Admin", "SystemAdmin", "UserManager"));

    options.AddPolicy("CanManageDocuments", policy =>
        policy.RequireRole("Admin", "Editor", "Contributor"));

    options.AddPolicy("CanManageWorkflows", policy =>
        policy.RequireRole("Admin", "WorkflowManager"));
});

var allowedOrigins = builder.Configuration.GetSection("CORS:AllowedOrigins").Get<string[]>();
if (allowedOrigins is { Length: > 0 })
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ApiCorsPolicy", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}
else if (builder.Environment.IsDevelopment())
{
    var devOrigins = new[] { "http://localhost:3000", "http://localhost:5000", "http://localhost:5143" };
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ApiCorsPolicy", policy =>
        {
            policy.WithOrigins(devOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
    Log.Warning("CORS:AllowedOrigins not configured. Using development origins only.");
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ApiCorsPolicy", policy =>
        {
            policy.SetIsOriginAllowed(_ => false);
        });
    });
    Log.Error("CORS:AllowedOrigins not configured in non-development environment. No origins will be allowed.");
}

builder.Services.AddAntiforgery();
builder.Services.AddSignalR();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<TenantAuthorizationFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddScoped<IStreamingNotificationService, SignalRStreamingService>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddScoped<IWorkflowBridge, WorkflowBridge>();
else
    builder.Services.AddScoped<IWorkflowBridge, NoOpWorkflowBridge>();
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json", "application/json-seq"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["services"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["services"])
    .AddCheck<AiProviderHealthCheck>("ai-providers", tags: ["services"])
    .AddCheck<MemoryHealthCheck>("memory", tags: ["resources"]);

var otelEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(DiagnosticsConfig.ServiceName, serviceVersion: DiagnosticsConfig.ServiceVersion))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddSource(DiagnosticsConfig.ActivitySource.Name);

        if (!string.IsNullOrEmpty(otelEndpoint))
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
        else
            tracing.AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel");

        if (!string.IsNullOrEmpty(otelEndpoint))
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
        else
            metrics.AddConsoleExporter();
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    if (!string.IsNullOrEmpty(otelEndpoint))
        logging.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "R2WAI API",
        Version = "v1",
        Description = "R2WAI Enterprise Platform - AI-Powered Work Execution Platform"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key authentication for external access. Pass your key in the X-API-Key header.",
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "R2WAI API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("ApiCorsPolicy");
app.UseAuthentication();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

if (!string.IsNullOrEmpty(elsaConnectionString) && !app.Environment.IsEnvironment("Testing"))
    app.UseWorkflowsApi();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<StatusHub>("/hubs/status");
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHealthChecks("/health/startup", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds
        });
        await ctx.Response.WriteAsync(json);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("services") || check.Tags.Contains("database"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                tags = e.Value.Tags
            })
        });
        await ctx.Response.WriteAsync(json);
    }
});

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                tags = e.Value.Tags,
                exception = e.Value.Exception?.Message
            })
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await ctx.Response.WriteAsync(json);
    }
});

try
{
    if (!app.Environment.IsEnvironment("Testing"))
    {
        ValidateProductionConfig(app);

        try
        {
            Log.Information("Initializing database...");
            await DatabaseInitializer.InitializeAsync(app.Services);
        }
        catch (Exception dbEx)
        {
            Log.Warning(dbEx, "Database initialization failed — API will start without a database. Ensure PostgreSQL is running.");
        }
    }
    Log.Information("Starting R2WAI API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void ValidateProductionConfig(WebApplication app)
{
    var config = app.Configuration;
    var warnings = new List<string>();
    var errors = new List<string>();

    var connectionString = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
        errors.Add("Database connection string (ConnectionStrings:DefaultConnection) is not configured");
    else if (connectionString.Contains("CHANGE_ME") || connectionString.Contains("${"))
        errors.Add("Database connection string contains a placeholder value. Set a real connection string via ConnectionStrings__DefaultConnection env var");

    // Must match what JwtBearer middleware actually reads
    var jwtSecret = config["Authentication:Jwt:SecretKey"];
    if (string.IsNullOrEmpty(jwtSecret))
        errors.Add("JWT secret key is not configured. Set Authentication__Jwt__SecretKey environment variable");
    else if (jwtSecret.Contains("CHANGE_ME") || jwtSecret.StartsWith("${"))
        errors.Add("JWT secret key contains a placeholder value. Override via Authentication__Jwt__SecretKey env var");
    else if (jwtSecret.Length < 32)
        warnings.Add("JWT secret key is shorter than 32 characters — use a longer key in production");

    var encryptionKey = config["Security:EncryptionKey"];
    if (string.IsNullOrEmpty(encryptionKey))
        errors.Add("Encryption key is not configured. Set Security__EncryptionKey environment variable");
    else if (encryptionKey.Contains("CHANGE_ME") || encryptionKey.StartsWith("${"))
        errors.Add("Encryption key contains a placeholder value. Override via Security__EncryptionKey env var");

    var aiProvider = (config["AI:Provider"] ?? "openai").ToLowerInvariant();
    if (aiProvider == "openai")
    {
        var apiKey = config["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            warnings.Add("OpenAI API key is not configured. AI features will not work. Set OPENAI_API_KEY or switch to AI:Provider=ollama");
    }
    else if (aiProvider == "ollama")
    {
        var endpoint = config["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
        Log.Information("AI provider: Ollama at {Endpoint}", endpoint);
    }

    var smtpHost = config["Email:SmtpHost"];
    if (string.IsNullOrEmpty(smtpHost))
        warnings.Add("SMTP is not configured. Email notifications will be logged but not sent");

    foreach (var warning in warnings)
        Log.Warning("[Config] {Warning}", warning);

    if (errors.Count > 0)
    {
        foreach (var error in errors)
            Log.Error("[Config] {Error}", error);

        if (app.Environment.IsProduction())
            throw new InvalidOperationException(
                $"Production startup blocked: {errors.Count} configuration error(s). " +
                string.Join("; ", errors));
    }
}

public partial class Program { }
