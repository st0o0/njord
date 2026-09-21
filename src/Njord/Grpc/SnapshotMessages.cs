using Njord.Domain.Weather;

namespace Njord.Grpc;

// Commands
public sealed record UpdateForecast(string Location, WeatherModel Model, ModelForecast Forecast);
public sealed record UpdateEnrichment(string Location, string TypeName, object Result);

// Queries
public sealed record QueryForecast(string Location, string ModelId);
public sealed record QueryAllForecasts;
public sealed record QueryEnrichment(string Location, string TypeName);
public sealed record QueryAllEnrichments(string Location);

// Forecast responses
public abstract record ForecastQueryResponse;
public sealed record ForecastFound(ModelForecast Forecast) : ForecastQueryResponse;
public sealed record ForecastNotFound(string ModelKey) : ForecastQueryResponse;
public sealed record ForecastQueryFailed(Exception Cause) : ForecastQueryResponse;

public abstract record AllForecastsQueryResponse;
public sealed record AllForecastsResult(IReadOnlyDictionary<(string Location, string ModelId), ModelForecast> Forecasts) : AllForecastsQueryResponse;
public sealed record AllForecastsFailed(Exception Cause) : AllForecastsQueryResponse;

// Enrichment responses
public abstract record EnrichmentQueryResponse;
public sealed record EnrichmentFound(object Result) : EnrichmentQueryResponse;
public sealed record EnrichmentNotFound(string Key) : EnrichmentQueryResponse;
public sealed record EnrichmentQueryFailed(Exception Cause) : EnrichmentQueryResponse;

public abstract record AllEnrichmentsQueryResponse;
public sealed record AllEnrichmentsResult(IReadOnlyList<(string TypeName, object Value)> Results) : AllEnrichmentsQueryResponse;
public sealed record AllEnrichmentsFailed(Exception Cause) : AllEnrichmentsQueryResponse;
