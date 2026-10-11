using Njord.Weather;

namespace Njord.Messages.Snapshots;

public sealed record QueryAllForecasts;

public abstract record QueryAllForecastsResponse;
public sealed record QueryAllForecastsCompleted(IReadOnlyDictionary<(string Location, string ModelId), Forecast> Forecasts) : QueryAllForecastsResponse;
public sealed record QueryAllForecastsFailed(Exception Cause) : QueryAllForecastsResponse;
