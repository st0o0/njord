namespace Njord.Messages.Fetch;

public enum FetchRejectionReason
{
    QueueFull,
    Budget,
    PipelineUnavailable,
    Expired,
}
