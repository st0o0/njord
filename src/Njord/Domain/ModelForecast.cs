namespace Njord.Domain;

public sealed record ModelForecast(
    WeatherModel Model,
    string Location,
    CycleId Cycle,
    DateTimeOffset RetrievedAt,
    ForecastSeries Series);
