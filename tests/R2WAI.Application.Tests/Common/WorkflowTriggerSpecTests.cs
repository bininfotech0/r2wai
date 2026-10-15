using R2WAI.Application.Features.Workflows;

namespace R2WAI.Application.Tests.Common;

public class WorkflowTriggerSpecTests
{
    [Theory]
    [InlineData(null, WorkflowTriggerKind.Manual)]
    [InlineData("", WorkflowTriggerKind.Manual)]
    [InlineData("Manual", WorkflowTriggerKind.Manual)]
    [InlineData("webhook", WorkflowTriggerKind.Webhook)]
    [InlineData("Schedule", WorkflowTriggerKind.Schedule)]
    [InlineData("schedule:0 9 * * 1-5|Asia/Kolkata", WorkflowTriggerKind.Schedule)]
    [InlineData("Payment Received", WorkflowTriggerKind.Unsupported)]
    [InlineData("Form Submitted", WorkflowTriggerKind.Unsupported)]
    public void Parse_RecognisesOnlyTriggersThatStartARun(string? trigger, WorkflowTriggerKind expected)
    {
        Assert.Equal(expected, WorkflowTriggerSpec.Parse(trigger).Kind);
    }

    [Fact]
    public void Parse_SplitsCronAndTimeZone_DefaultingToUtc()
    {
        var withZone = WorkflowTriggerSpec.Parse("schedule:0 9 * * 1-5|Asia/Kolkata");
        Assert.Equal("0 9 * * 1-5", withZone.Cron);
        Assert.Equal("Asia/Kolkata", withZone.TimeZoneId);

        var noZone = WorkflowTriggerSpec.Parse("schedule:*/15 * * * *");
        Assert.Equal("*/15 * * * *", noZone.Cron);
        Assert.Equal("UTC", noZone.TimeZoneId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Manual")]
    [InlineData("Webhook")]
    [InlineData("schedule:0 9 * * 1-5|Asia/Kolkata")]
    [InlineData("schedule:30 18 1 * *|UTC")]
    public void Validate_AcceptsRunnableTriggers(string? trigger)
    {
        Assert.Null(WorkflowTriggerSpec.Validate(trigger));
    }

    [Theory]
    [InlineData("Application Submitted", "not supported")]
    [InlineData("Schedule", "Choose when")]
    [InlineData("schedule:|UTC", "Choose when")]
    [InlineData("schedule:every morning|UTC", "not a valid schedule")]
    [InlineData("schedule:0 9 * * *|Mars/Olympus", "Unknown time zone")]
    public void Validate_RejectsTriggersThatWouldNeverFire(string trigger, string expectedFragment)
    {
        var error = WorkflowTriggerSpec.Validate(trigger);
        Assert.NotNull(error);
        Assert.Contains(expectedFragment, error);
    }

    [Fact]
    public void Validate_RejectsValuesLongerThanTheColumn()
    {
        var tooLong = WorkflowTriggerSpec.FormatSchedule("0 9 * * *", new string('x', 100));
        Assert.Contains("100 characters", WorkflowTriggerSpec.Validate(tooLong));
    }

    [Fact]
    public void OccurrencesBetween_UsesTheScheduleTimeZone_AndExcludesTheStart()
    {
        // 09:00 in Asia/Kolkata (UTC+5:30) is 03:30 UTC.
        var spec = WorkflowTriggerSpec.Parse("schedule:0 9 * * *|Asia/Kolkata");
        var from = new DateTime(2026, 10, 16, 3, 30, 0, DateTimeKind.Utc);
        var to = from.AddDays(2);

        var occurrences = spec.OccurrencesBetween(from, to).ToList();

        Assert.Equal(
            [new DateTime(2026, 10, 17, 3, 30, 0, DateTimeKind.Utc), new DateTime(2026, 10, 18, 3, 30, 0, DateTimeKind.Utc)],
            occurrences);
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("Schedule")]
    [InlineData("schedule:not a cron|UTC")]
    public void OccurrencesBetween_IsEmptyForAnythingNotAValidSchedule(string trigger)
    {
        var now = DateTime.UtcNow;
        Assert.Empty(WorkflowTriggerSpec.Parse(trigger).OccurrencesBetween(now.AddDays(-1), now));
    }
}
