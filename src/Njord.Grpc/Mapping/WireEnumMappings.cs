using Njord.Model;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAirQualityAlertType = Njord.Messages.Alerts.AirQualityAlertType;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainFloodAlertType = Njord.Messages.Alerts.FloodAlertType;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoDomain = Njord.Grpc.V2.Domain;
using ProtoAlertType = Njord.Grpc.V2.AlertType;
using ProtoPollPhase = Njord.Grpc.V2.PollPhase;

namespace Njord.Grpc.Mapping;

internal static class WireEnumMappings
{
    public static ProtoPollPhase ToProto(this DomainPollPhase phase) => phase switch
    {
        DomainPollPhase.Discovery => ProtoPollPhase.Discovery,
        DomainPollPhase.Steady => ProtoPollPhase.Steady,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static ProtoDomain ToProto(this DomainKind domain) => domain switch
    {
        DomainKind.Weather => ProtoDomain.Weather,
        DomainKind.AirQuality => ProtoDomain.AirQuality,
        DomainKind.Flood => ProtoDomain.Flood,
        DomainKind.Solar => ProtoDomain.Solar,
        DomainKind.Marine => ProtoDomain.Marine,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };

    public static DomainKind ToDomain(this ProtoDomain domain) => domain switch
    {
        ProtoDomain.Weather => DomainKind.Weather,
        ProtoDomain.AirQuality => DomainKind.AirQuality,
        ProtoDomain.Flood => DomainKind.Flood,
        ProtoDomain.Solar => DomainKind.Solar,
        ProtoDomain.Marine => DomainKind.Marine,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };

    public static ProtoAlertType ToProto(this DomainAlertType type) => type switch
    {
        DomainAlertType.Frost => ProtoAlertType.Frost,
        DomainAlertType.Heat => ProtoAlertType.Heat,
        DomainAlertType.Storm => ProtoAlertType.Storm,
        DomainAlertType.HeavyRain => ProtoAlertType.HeavyRain,
        DomainAlertType.Uv => ProtoAlertType.Uv,
        DomainAlertType.Fog => ProtoAlertType.Fog,
        DomainAlertType.Snow => ProtoAlertType.Snow,
        DomainAlertType.PressureDrop => ProtoAlertType.PressureDrop,
        DomainAlertType.Thunderstorm => ProtoAlertType.Thunderstorm,
        DomainAlertType.Ice => ProtoAlertType.Ice,
        DomainAlertType.WindChill => ProtoAlertType.WindChill,
        DomainAlertType.Visibility => ProtoAlertType.Visibility,
        DomainAlertType.TropicalNight => ProtoAlertType.TropicalNight,
        DomainAlertType.Humidity => ProtoAlertType.Humidity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static ProtoAlertType ToProto(this DomainAirQualityAlertType type) => type switch
    {
        DomainAirQualityAlertType.Pollen => ProtoAlertType.Pollen,
        DomainAirQualityAlertType.Pm25 => ProtoAlertType.Pm25,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static ProtoAlertType ToProto(this DomainFloodAlertType type) => type switch
    {
        DomainFloodAlertType.FloodRisk => ProtoAlertType.FloodRisk,
        DomainFloodAlertType.FloodRain => ProtoAlertType.FloodRain,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static ProtoAlertSeverity ToProto(this DomainAlertSeverity severity) => severity switch
    {
        DomainAlertSeverity.None => ProtoAlertSeverity.None,
        DomainAlertSeverity.Yellow => ProtoAlertSeverity.Yellow,
        DomainAlertSeverity.Orange => ProtoAlertSeverity.Orange,
        DomainAlertSeverity.Red => ProtoAlertSeverity.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };
}
