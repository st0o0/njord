using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Messages.Forecast;

public sealed record QueryForecast(LocationId Location, string ModelId);

public abstract record ForecastResponse;
public sealed record ForecastResult(WeatherForecast Forecast) : ForecastResponse;
public sealed record ForecastNotAvailable(string ModelKey) : ForecastResponse;
