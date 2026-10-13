using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Messages.Snapshots;

public sealed record QueryForecast(string Location, string ModelId);

public abstract record ForecastResponse;
public sealed record ForecastResult(WeatherForecast Forecast) : ForecastResponse;
public sealed record ForecastNotAvailable(string ModelKey) : ForecastResponse;
