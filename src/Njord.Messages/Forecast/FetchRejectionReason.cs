namespace Njord.Messages.Forecast;

public enum FetchRejectionReason
{
    QueueFull,
    Budget,
    PipelineUnavailable,
    Expired,
}
