using Njord.Weather;

namespace Njord.Messages.Snapshots;

public sealed record QueryForecast(string Location, string ModelId) : IWithModelKey;

public abstract record QueryForecastResponse;
public sealed record QueryForecastCompleted(ModelForecast Forecast) : QueryForecastResponse;
public sealed record QueryForecastNotAvailable(string ModelKey) : QueryForecastResponse;
public sealed record QueryForecastFailed(Exception Cause) : QueryForecastResponse;
