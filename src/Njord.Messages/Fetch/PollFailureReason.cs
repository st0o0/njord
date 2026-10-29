namespace Njord.Messages.Fetch;

public enum PollFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}
