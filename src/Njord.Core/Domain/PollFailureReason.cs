namespace Njord.Core.Domain;

public enum PollFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}
