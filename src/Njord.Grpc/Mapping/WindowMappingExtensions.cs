using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class WindowMappingExtensions
{
    internal static V2.TimeWindow ToProto(this ActivityWindow window) => new()
    {
        Start = window.Start.ToTimestamp(),
        End = window.End.ToTimestamp(),
        Score = window.Score,
        Limiting = window.Limiting?.ToProto()
    };

    internal static V2.LimitingFactor ToProto(this LimitingFactor factor) => new()
    {
        Kind = factor.Kind.ToProto(),
        From = factor.From.ToTimestamp(),
        Value = factor.Value
    };

    internal static V2.LimitingKind ToProto(this LimitingKind kind) => kind switch
    {
        LimitingKind.None => V2.LimitingKind.Unspecified,
        LimitingKind.Rain => V2.LimitingKind.Rain,
        LimitingKind.Uv => V2.LimitingKind.Uv,
        LimitingKind.Wind => V2.LimitingKind.Wind,
        LimitingKind.TemperatureHigh => V2.LimitingKind.TemperatureHigh,
        LimitingKind.TemperatureLow => V2.LimitingKind.TemperatureLow,
        LimitingKind.Humidity => V2.LimitingKind.Humidity,
        LimitingKind.Pollen => V2.LimitingKind.Pollen,
        LimitingKind.AirQuality => V2.LimitingKind.AirQuality,
        LimitingKind.Visibility => V2.LimitingKind.Visibility,
        LimitingKind.Thunderstorm => V2.LimitingKind.Thunderstorm,
        LimitingKind.Darkness => V2.LimitingKind.Darkness,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
