using System.Text.Json.Serialization;
using Njord.Messages.Trends;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

[JsonConverter(typeof(TrendPayloadConverter))]
public sealed record TrendPayload(
    IReadOnlyList<TrendParameterPayload> Parameters,
    string? WeatherChange,
    int? PrecipStarts,
    int? PrecipEnds,
    int? TempMaxIn,
    int? TempMinIn,
    Stability? Stability,
    double? StabilityRatio,
    double? DecayRate,
    int? ReliableHours);
