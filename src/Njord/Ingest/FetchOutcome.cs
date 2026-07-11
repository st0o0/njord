using Njord.Domain;

namespace Njord.Ingest;

public enum FetchFailureReason
{
    AuthFailed,
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

    /// <summary><paramref name="Detail"/> must never contain the API key.</summary>
    public sealed record Failure(
        CycleId Cycle,
        string Location,
        WeatherModel Model,
        FetchFailureReason Reason,
        string Detail) : FetchOutcome;
}
