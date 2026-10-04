using Njord.Domain.Weather;

namespace Njord.Messages.Snapshots;

// Commands
public sealed record UpdateForecast(string Location, WeatherModel Model, ModelForecast Forecast) : IWithModelKey
{
    string IWithModelKey.ModelId => Model.Id;
}

public sealed record UpdateEnrichment(string Location, string TypeName, object Result) : IWithEnrichmentKey;

// Queries
public sealed record QueryForecast(string Location, string ModelId) : IWithModelKey;
public sealed record QueryAllForecasts;
public sealed record QueryEnrichment(string Location, string TypeName) : IWithEnrichmentKey;
public sealed record QueryAllEnrichments(string Location) : IWithLocation;

// Forecast responses
public abstract record QueryForecastResponse;
public sealed record ForecastFound(ModelForecast Forecast) : QueryForecastResponse;
public sealed record ForecastNotFound(string ModelKey) : QueryForecastResponse;
public sealed record QueryForecastFailed(Exception Cause) : QueryForecastResponse;

public abstract record QueryAllForecastsResponse;
public sealed record QueryAllForecastsResult(IReadOnlyDictionary<(string Location, string ModelId), ModelForecast> Forecasts) : QueryAllForecastsResponse;
public sealed record QueryAllForecastsFailed(Exception Cause) : QueryAllForecastsResponse;

// Enrichment responses
public abstract record QueryEnrichmentResponse;
public sealed record EnrichmentFound(object Result) : QueryEnrichmentResponse;
public sealed record EnrichmentNotFound(string Key) : QueryEnrichmentResponse;
public sealed record QueryEnrichmentFailed(Exception Cause) : QueryEnrichmentResponse;

public abstract record QueryAllEnrichmentsResponse;
public sealed record QueryAllEnrichmentsResult(IReadOnlyList<(string TypeName, object Value)> Results) : QueryAllEnrichmentsResponse;
public sealed record QueryAllEnrichmentsFailed(Exception Cause) : QueryAllEnrichmentsResponse;
