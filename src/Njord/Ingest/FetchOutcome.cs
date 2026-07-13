using Njord.Domain;

namespace Njord.Ingest;

public enum FetchFailureReason
{
    RateLimited,
    ModelUnavailable,
    MalformedPayload,
    Transport,
}

/// <summary>Result of one fetch attempt. Expected failures are data, not exceptions.</summary>
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
