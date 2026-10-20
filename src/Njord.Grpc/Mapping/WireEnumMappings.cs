using Njord.Model;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoAlertType = Njord.Grpc.V2.AlertType;
using ProtoDomain = Njord.Grpc.V2.Domain;
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
        DomainAlertType.Pollen => ProtoAlertType.Pollen,
        DomainAlertType.Pm25 => ProtoAlertType.Pm25,
        DomainAlertType.FloodRisk => ProtoAlertType.FloodRisk,
        DomainAlertType.FloodRain => ProtoAlertType.FloodRain,
        DomainAlertType.SwimmingConditions => ProtoAlertType.SwimmingConditions,
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
