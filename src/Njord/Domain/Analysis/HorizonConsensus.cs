using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record HorizonConsensus(
    [property: JsonProperty("median")] double? Median,
    [property: JsonProperty("trimmedMean")] double? TrimmedMean,
    [property: JsonProperty("spread")] double? Spread,
    [property: JsonProperty("iqr")] double? Iqr,
    [property: JsonProperty("agreement")] double? Agreement,
    [property: JsonProperty("outlier")] OutlierInfo? Outlier,
    [property: JsonProperty("confidenceInterval")] ConfidenceIntervalInfo? ConfidenceInterval,
    [property: JsonProperty("availableModels")] IReadOnlyList<WeatherModel> AvailableModels);
