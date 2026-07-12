namespace Njord.Domain;

public sealed record ModelForecast(
    WeatherModel Model,
    string Location,
    CycleId Cycle,
    ForecastSeries Hourly,
    DailyForecastSeries Daily);
