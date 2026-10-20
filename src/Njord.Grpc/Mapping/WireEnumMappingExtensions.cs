using Njord.Forecast;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoDomain = Njord.Grpc.V2.Domain;
using ProtoPollPhase = Njord.Grpc.V2.PollPhase;

namespace Njord.Grpc.Mapping;

internal static class WireEnumMappingExtensions
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
        DomainKind.Marine => ProtoDomain.Marine,
        DomainKind.Seasonal => ProtoDomain.Seasonal,
        DomainKind.Satellite => ProtoDomain.Satellite,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };

    public static DomainKind ToDomain(this ProtoDomain domain) => domain switch
    {
        ProtoDomain.Weather => DomainKind.Weather,
        ProtoDomain.AirQuality => DomainKind.AirQuality,
        ProtoDomain.Flood => DomainKind.Flood,
        ProtoDomain.Marine => DomainKind.Marine,
        ProtoDomain.Seasonal => DomainKind.Seasonal,
        ProtoDomain.Satellite => DomainKind.Satellite,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
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
