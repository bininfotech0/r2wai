namespace R2WAI.Domain.Tests.Entities;

public class BackgroundJobTests
{
    [Fact]
    public void Create_SetsPendingStatusAndImmediateNextAttempt()
    {
        var before = DateTime.UtcNow;
        var job = new BackgroundJob(Guid.NewGuid(), "IndexDocument", "{}", maxAttempts: 3);
        var after = DateTime.UtcNow;

        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Equal(0, job.Attempts);
        Assert.Equal(3, job.MaxAttempts);
        Assert.Null(job.LastError);
        Assert.Null(job.ProcessedAt);
        Assert.InRange(job.NextAttemptAt, before, after);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public void MarkSucceeded_SetsStatusAndProcessedAt()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "IndexDocument", "{}");

        job.MarkSucceeded();

        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
        Assert.NotNull(job.ProcessedAt);
    }

    [Fact]
    public void MarkFailed_BelowMaxAttempts_RequeuesWithBackoffDelay()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "IndexDocument", "{}", maxAttempts: 5);
        var before = DateTime.UtcNow;

        job.MarkFailed("transient error");

        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal("transient error", job.LastError);
        Assert.Null(job.ProcessedAt);
        Assert.True(job.NextAttemptAt > before.AddSeconds(29)); // first backoff step is 30s
    }

    [Fact]
    public void MarkFailed_AtMaxAttempts_DeadLetters()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "IndexDocument", "{}", maxAttempts: 2);

        job.MarkFailed("first failure");
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);

        job.MarkFailed("second failure");

        Assert.Equal(BackgroundJobStatus.DeadLettered, job.Status);
        Assert.Equal(2, job.Attempts);
        Assert.Equal("second failure", job.LastError);
        Assert.NotNull(job.ProcessedAt);
    }

    [Fact]
    public void MarkFailed_LongError_IsTruncatedTo2000Chars()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "IndexDocument", "{}");

        job.MarkFailed(new string('x', 3000));

        Assert.Equal(2000, job.LastError!.Length);
    }

    [Fact]
    public void MarkUnroutable_DeadLettersImmediatelyRegardlessOfMaxAttempts()
    {
        var job = new BackgroundJob(Guid.NewGuid(), "NoSuchHandler", "{}", maxAttempts: 5);

        job.MarkUnroutable("no handler registered");

        Assert.Equal(BackgroundJobStatus.DeadLettered, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal("no handler registered", job.LastError);
        Assert.NotNull(job.ProcessedAt);
    }
}
