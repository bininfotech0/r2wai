namespace R2WAI.Domain.Enums;

public enum BackgroundJobStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    DeadLettered
}
