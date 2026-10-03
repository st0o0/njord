using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record HistoryResult(
    string Location,
    Dictionary<WeatherModel, double?> Mae7d,
    Dictionary<WeatherModel, double?> Mae30d,
    Dictionary<WeatherModel, double> Weights,
    Dictionary<WeatherModel, double?> Drift,
    WeatherModel? SeasonalBest,
    (bool IsAnomaly, double DeviationSigma)? Anomaly,
    double? WeightedTemperature);
