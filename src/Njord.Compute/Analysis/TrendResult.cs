using Newtonsoft.Json;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record TrendResult(
    [property: JsonProperty("location")] LocationId Location,
    [property: JsonProperty("parameterTrends")] IReadOnlyDictionary<string, ParameterTrend?> ParameterTrends,
    [property: JsonProperty("weatherChange")] WeatherChangeResult? WeatherChange,
    [property: JsonProperty("precipTiming")] PrecipTimingInfo PrecipTiming,
    [property: JsonProperty("extremaTiming")] ExtremaTimingInfo ExtremaTiming,
    [property: JsonProperty("stability")] StabilityInfo? Stability,
    [property: JsonProperty("decay")] DecayInfo? Decay);
