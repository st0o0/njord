using Njord.Weather;

namespace Njord.Messages.Forecast;

public sealed record FetchForecast(LocationId Location, string ModelId);

public abstract record FetchForecastResponse;
