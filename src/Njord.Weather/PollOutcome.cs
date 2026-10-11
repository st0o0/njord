namespace Njord.Weather;

public enum PollFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}

public abstract record PollOutcome
{
    private PollOutcome() { }

    public sealed record Success(Forecast Forecast) : PollOutcome;

    public sealed record Failure(
        string Location,
        WeatherModel Model,
        PollFailureReason Reason,
        string Detail) : PollOutcome;
}
