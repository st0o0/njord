namespace Njord.Messages.Forecast;

public enum PollFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}
