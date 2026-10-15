using Cronos;

namespace R2WAI.Application.Features.Workflows;

public enum WorkflowTriggerKind
{
    Manual,
    Webhook,
    Schedule,
    Unsupported,
}

/// <summary>
/// The one parser for <c>Workflow.Trigger</c>. Only triggers something actually starts are valid:
/// Manual (Run/Test, or an agent's start_workflow tool), Webhook (WebhookEndpoint rows) and Schedule
/// (ScheduledWorkflowTriggerService). Event labels the wizard used to offer — "Application Submitted",
/// "Payment Received", "Form Submitted"… — had no event source behind them and never fired, so they
/// parse as <see cref="WorkflowTriggerKind.Unsupported"/> and are rejected on save.
///
/// A schedule is stored inline in the existing Trigger column as <c>schedule:&lt;cron&gt;|&lt;IANA tz&gt;</c>
/// (5-field cron, e.g. <c>schedule:0 9 * * 1-5|Asia/Kolkata</c>) so no schema change is needed.
/// A bare "Schedule" (what the old wizard saved) has no cron and is treated as not yet configured.
/// </summary>
public sealed record WorkflowTriggerSpec(WorkflowTriggerKind Kind, string? Cron = null, string? TimeZoneId = null)
{
    public const string SchedulePrefix = "schedule:";
    public const int MaxLength = 100;

    public static WorkflowTriggerSpec Parse(string? trigger)
    {
        var value = trigger?.Trim();
        if (string.IsNullOrEmpty(value) || value.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            return new(WorkflowTriggerKind.Manual);
        if (value.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
            return new(WorkflowTriggerKind.Webhook);
        if (value.Equals("Schedule", StringComparison.OrdinalIgnoreCase))
            return new(WorkflowTriggerKind.Schedule);
        if (value.StartsWith(SchedulePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var body = value[SchedulePrefix.Length..];
            var bar = body.LastIndexOf('|');
            var cron = (bar >= 0 ? body[..bar] : body).Trim();
            var tz = bar >= 0 ? body[(bar + 1)..].Trim() : null;
            return new(WorkflowTriggerKind.Schedule, cron.Length == 0 ? null : cron, string.IsNullOrEmpty(tz) ? "UTC" : tz);
        }

        return new(WorkflowTriggerKind.Unsupported);
    }

    public static string FormatSchedule(string cron, string timeZoneId) =>
        $"{SchedulePrefix}{cron.Trim()}|{timeZoneId.Trim()}";

    /// <summary>Null when the stored value is something a run can actually be started by.</summary>
    public static string? Validate(string? trigger)
    {
        if (trigger is { Length: > MaxLength })
            return $"Trigger must be {MaxLength} characters or fewer.";

        var spec = Parse(trigger);
        switch (spec.Kind)
        {
            case WorkflowTriggerKind.Unsupported:
                return $"Trigger '{trigger}' is not supported. Use Manual, Webhook, or a schedule.";
            case WorkflowTriggerKind.Schedule when spec.Cron is null:
                return "Choose when this schedule runs.";
            case WorkflowTriggerKind.Schedule:
                if (!TryParseCron(spec.Cron!, out _))
                    return $"'{spec.Cron}' is not a valid schedule. Use 5-field cron, e.g. '0 9 * * 1-5' for weekdays at 09:00.";
                if (!TryFindTimeZone(spec.TimeZoneId!, out _))
                    return $"Unknown time zone '{spec.TimeZoneId}'. Use an IANA name such as 'Asia/Kolkata' or 'UTC'.";
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Occurrences in (<paramref name="fromUtc"/>, <paramref name="toUtc"/>], in UTC. Empty for anything
    /// that is not a fully valid schedule.
    /// </summary>
    public IEnumerable<DateTime> OccurrencesBetween(DateTime fromUtc, DateTime toUtc)
    {
        if (Kind != WorkflowTriggerKind.Schedule || Cron is null
            || !TryParseCron(Cron, out var expression) || !TryFindTimeZone(TimeZoneId ?? "UTC", out var zone))
            return [];

        return expression!.GetOccurrences(
            DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
            zone!, fromInclusive: false, toInclusive: true);
    }

    private static bool TryParseCron(string cron, out CronExpression? expression) =>
        CronExpression.TryParse(cron, CronFormat.Standard, out expression);

    private static bool TryFindTimeZone(string id, out TimeZoneInfo? zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = null;
            return false;
        }
    }
}
