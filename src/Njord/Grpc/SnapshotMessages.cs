using Njord.Domain.Weather;

namespace Njord.Grpc;

// Commands
public sealed record UpdateForecast(string Location, WeatherModel Model, ModelForecast Forecast);
public sealed record UpdateEnrichment(string Location, string TypeName, object Result);

// Queries
public sealed record GetForecast(string Location, string ModelId);
public sealed record GetAllForecasts;
public sealed record GetEnrichment(string Location, string TypeName);
public sealed record GetAllEnrichments(string Location);

// Responses
public sealed record ForecastResponse(ModelForecast? Forecast);
public sealed record AllForecastsResponse(IReadOnlyDictionary<(string Location, string ModelId), ModelForecast> Forecasts);
public sealed record EnrichmentResponse(object? Result);
public sealed record AllEnrichmentsResponse(IReadOnlyList<(string TypeName, object Result)> Results);

// Ack
public sealed record Ack;
