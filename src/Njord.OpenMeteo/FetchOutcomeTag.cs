using Njord.Forecast;

namespace Njord.OpenMeteo;

internal static class FetchOutcomeTag
{
    internal static string For(PollOutcome outcome) => outcome switch
    {
        PollOutcome.Success => "success",
        PollOutcome.Failure f => For(f.Reason),
        _ => "unknown",
    };

    internal static string For(PollFailureReason reason) => reason switch
    {
        PollFailureReason.RateLimited => "ratelimited",
        PollFailureReason.ModelUnavailable => "modelunavailable",
        PollFailureReason.MalformedPayload => "malformedpayload",
        PollFailureReason.Transport => "transport",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };
}
