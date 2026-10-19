namespace Njord.Model;

public enum PollFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}
