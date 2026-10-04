using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.Services.BackgroundJobs;

/// <summary>
/// Deliberately does NOT take the ambient/request-scoped ApplicationDbContext (e.g. via
/// IRepository&lt;BackgroundJob&gt;) -- EnqueueAsync is routinely called from inside a domain-event
/// handler (DocumentUploadedEventHandler, itself published from ApplicationDbContext.SaveChangesAsync's
/// own DispatchDomainEventsAsync step, BEFORE that entity's domain events are cleared). Calling
/// SaveChangesAsync on that same ambient context here would recursively re-enter the override,
/// re-scan the still-dirty ChangeTracker, re-find the same not-yet-cleared event, and re-publish it --
/// an unbounded self-amplifying loop. Confirmed live: one document upload produced 5000+ duplicate
/// BackgroundJob rows and pegged the request forever before this fix. Exact same failure mode and fix
/// as NotificationService.SendAsync's isolated-scope pattern -- see its comment for the fuller writeup.
/// An isolated DbContext has an empty ChangeTracker, so it can never re-trigger anything.
/// </summary>
public sealed class BackgroundJobQueue(IServiceScopeFactory scopeFactory) : IBackgroundJobQueue
{
    public async Task EnqueueAsync(string jobType, object payload, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var job = new BackgroundJob(Guid.NewGuid(), jobType, JsonSerializer.Serialize(payload));
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(ct);
    }
}
