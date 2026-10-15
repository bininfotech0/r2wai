namespace R2WAI.Domain.Tests.Entities;

public class UserMemoryTests
{
    private static UserMemory Create(UserMemoryStatus status = UserMemoryStatus.Active, string? key = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, UserMemoryCategory.Fact, key,
            "  Works in procurement.  ", UserMemorySource.Manual, status);

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("___", null)]
    [InlineData("preferred_language", "preferred_language")]
    [InlineData("  Preferred Language ", "preferred_language")]
    [InlineData("Job-Role / Title", "job_role_title")]
    public void NormalizeKey_IsLowerSnakeCase_OrNull(string? input, string? expected) =>
        Assert.Equal(expected, UserMemory.NormalizeKey(input));

    [Fact]
    public void NormalizeKey_IsCappedAtTheColumnLength() =>
        Assert.Equal(UserMemory.MaxKeyLength, UserMemory.NormalizeKey(new string('a', 100))!.Length);

    [Fact]
    public void Constructor_TrimsContent_AndNormalizesKey()
    {
        var memory = Create(key: "Team Name");

        Assert.Equal("Works in procurement.", memory.Content);
        Assert.Equal("team_name", memory.Key);
    }

    [Fact]
    public void SetPaused_TogglesBetweenActiveAndPaused()
    {
        var memory = Create();

        memory.SetPaused(true);
        Assert.Equal(UserMemoryStatus.Paused, memory.Status);

        memory.SetPaused(false);
        Assert.Equal(UserMemoryStatus.Active, memory.Status);
    }

    [Fact]
    public void PendingReview_OnlyBecomesActiveThroughApprove()
    {
        var memory = Create(UserMemoryStatus.PendingReview);

        // Resuming must not be a back door past review.
        memory.SetPaused(false);
        Assert.Equal(UserMemoryStatus.PendingReview, memory.Status);

        memory.Approve();
        Assert.Equal(UserMemoryStatus.Active, memory.Status);
    }

    [Fact]
    public void Approve_DoesNotReactivateAPausedMemory()
    {
        var memory = Create(UserMemoryStatus.Paused);

        memory.Approve();

        Assert.Equal(UserMemoryStatus.Paused, memory.Status);
    }

    [Theory]
    [InlineData(null, true, false)]
    [InlineData("Read", true, false)]
    [InlineData("ReadWrite", true, true)]
    [InlineData("readwrite", true, true)]
    [InlineData("Off", false, false)]
    [InlineData("off", false, false)]
    public void AgentMemoryMode_DefaultsToRead(string? mode, bool reads, bool writes)
    {
        var settings = mode is null ? null : new AssistantBehaviorSettings { UserMemory = mode };

        Assert.Equal(reads, AssistantBehaviorSettings.ReadsUserMemory(settings));
        Assert.Equal(writes, AssistantBehaviorSettings.WritesUserMemory(settings));
    }
}
