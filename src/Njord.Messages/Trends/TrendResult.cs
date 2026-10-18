using Njord.Weather;

namespace Njord.Messages.Trends;

public sealed record TrendResult(
    LocationId Location,
    IReadOnlyDictionary<string, ParameterTrend?> ParameterTrends,
    WeatherChangeResult? WeatherChange,
    PrecipTimingInfo PrecipTiming,
    ExtremaTimingInfo ExtremaTiming,
    StabilityInfo? Stability,
    DecayInfo? Decay);
