using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record TrendResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("parameterTrends")] IReadOnlyDictionary<string, ParameterTrend?> ParameterTrends,
    [property: JsonProperty("weatherChange")] WeatherChangeResult? WeatherChange,
    [property: JsonProperty("precipTiming")] PrecipTimingInfo PrecipTiming,
    [property: JsonProperty("extremaTiming")] ExtremaTimingInfo ExtremaTiming,
    [property: JsonProperty("stability")] StabilityInfo? Stability,
    [property: JsonProperty("decay")] DecayInfo? Decay);
