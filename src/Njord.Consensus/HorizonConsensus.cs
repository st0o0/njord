using Njord.Weather;

namespace Njord.Consensus;

public sealed record HorizonConsensus(
    double? Median,
    double? TrimmedMean,
    double? Spread,
    double? Iqr,
    double? Agreement,
    OutlierInfo? Outlier,
    ConfidenceIntervalInfo? ConfidenceInterval,
    IReadOnlyList<WeatherModel> AvailableModels);
