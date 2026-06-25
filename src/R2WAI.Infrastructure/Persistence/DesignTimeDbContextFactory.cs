using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../R2WAI.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=r2wai;Username=r2wai;Password=r2wai_secret";

        builder.UseNpgsql(connectionString, b =>
            b.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName));

        return new ApplicationDbContext(
            builder.Options,
            new DesignTimeCurrentUserService(),
            new DesignTimeDateTimeService(),
            new DesignTimeMediator(),
            new DesignTimeLogger<ApplicationDbContext>());
    }
}

internal class DesignTimeCurrentUserService : ICurrentUserService
{
    public Guid? UserId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid? TenantId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string[] Roles => ["Admin"];
    public bool IsAuthenticated => true;
    public string? IpAddress => "127.0.0.1";
}

internal class DesignTimeDateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}

internal class DesignTimeMediator : MediatR.IMediator
{
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => Task.FromResult<TResponse>(default!);
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : MediatR.IRequest
        => Task.CompletedTask;
    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : MediatR.INotification
        => Task.CompletedTask;
}

internal class DesignTimeLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}