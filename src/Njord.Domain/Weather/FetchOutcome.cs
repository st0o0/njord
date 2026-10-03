namespace Njord.Domain.Weather;

public enum FetchFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}

public abstract record FetchOutcome
{
    private FetchOutcome() { }

    public sealed record Success(ModelForecast Forecast) : FetchOutcome;

    public sealed record Failure(
        string Location,
        WeatherModel Model,
        FetchFailureReason Reason,
        string Detail) : FetchOutcome;
}
