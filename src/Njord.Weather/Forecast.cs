namespace Njord.Weather;

public sealed record Forecast(
    WeatherModel Model,
    string Location,
    CycleId Cycle,
    ForecastSeries Hourly,
    DailyForecastSeries Daily);
