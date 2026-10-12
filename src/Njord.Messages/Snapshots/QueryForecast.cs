using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Messages.Snapshots;

public sealed record QueryForecast(string Location, string ModelId);

public abstract record QueryForecastResponse;
public sealed record QueryForecastCompleted(WeatherForecast Forecast) : QueryForecastResponse;
public sealed record QueryForecastNotAvailable(string ModelKey) : QueryForecastResponse;
