using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record OutlierInfo(
    [property: JsonProperty("model")] WeatherModel Model,
    [property: JsonProperty("deviation")] double Deviation);

public sealed record ConfidenceIntervalInfo(
    [property: JsonProperty("lower")] double Lower,
    [property: JsonProperty("upper")] double Upper);

public sealed record HorizonConsensus(
    [property: JsonProperty("median")] double? Median,
    [property: JsonProperty("trimmedMean")] double? TrimmedMean,
    [property: JsonProperty("spread")] double? Spread,
    [property: JsonProperty("iqr")] double? Iqr,
    [property: JsonProperty("agreement")] double? Agreement,
    [property: JsonProperty("outlier")] OutlierInfo? Outlier,
    [property: JsonProperty("confidenceInterval")] ConfidenceIntervalInfo? ConfidenceInterval,
    [property: JsonProperty("availableModels")] IReadOnlyList<WeatherModel> AvailableModels);

public sealed record ParameterConsensus(
    [property: JsonProperty("parameter")] ParameterDef Parameter,
    [property: JsonProperty("byHorizon")] IReadOnlyDictionary<string, HorizonConsensus> ByHorizon);

[method: JsonConstructor]
public sealed record ConsensusResult(
    [property: JsonProperty("parameters")] IReadOnlyList<ParameterConsensus> Parameters,
    [property: JsonProperty("dailyParameters")] IReadOnlyList<ParameterConsensus> DailyParameters)
{
    public ConsensusResult(IReadOnlyList<ParameterConsensus> parameters)
        : this(parameters, []) { }
}
