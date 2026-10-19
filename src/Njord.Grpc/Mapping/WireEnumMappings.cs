using Njord.Messages.Derived;
using Njord.Messages.Trends;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoAlertType = Njord.Grpc.V2.AlertType;
using ProtoDewPointComfort = Njord.Grpc.V2.DewPointComfort;
using ProtoPollPhase = Njord.Grpc.V2.PollPhase;
using ProtoStability = Njord.Grpc.V2.Stability;
using ProtoTrendDirection = Njord.Grpc.V2.TrendDirection;
using ProtoVpdCategory = Njord.Grpc.V2.VpdCategory;

namespace Njord.Grpc.Mapping;

internal static class WireEnumMappings
{
    public static ProtoPollPhase ToProto(this DomainPollPhase phase) => phase switch
    {
        DomainPollPhase.Discovery => ProtoPollPhase.Discovery,
        DomainPollPhase.Steady => ProtoPollPhase.Steady,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static ProtoTrendDirection ToProto(this TrendDirection direction) => direction switch
    {
        TrendDirection.Rising => ProtoTrendDirection.Rising,
        TrendDirection.Falling => ProtoTrendDirection.Falling,
        TrendDirection.Stable => ProtoTrendDirection.Stable,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    public static ProtoStability ToProto(this Stability stability) => stability switch
    {
        Stability.Converging => ProtoStability.Converging,
        Stability.Diverging => ProtoStability.Diverging,
        Stability.Stable => ProtoStability.Stable,
        _ => throw new ArgumentOutOfRangeException(nameof(stability), stability, null),
    };

    public static ProtoDewPointComfort ToProto(this DewPointComfort comfort) => comfort switch
    {
        DewPointComfort.Dry => ProtoDewPointComfort.Dry,
        DewPointComfort.Comfortable => ProtoDewPointComfort.Comfortable,
        DewPointComfort.Sticky => ProtoDewPointComfort.Sticky,
        DewPointComfort.Oppressive => ProtoDewPointComfort.Oppressive,
        DewPointComfort.Dangerous => ProtoDewPointComfort.Dangerous,
        _ => throw new ArgumentOutOfRangeException(nameof(comfort), comfort, null),
    };

    public static ProtoVpdCategory ToProto(this VpdCategory category) => category switch
    {
        VpdCategory.Low => ProtoVpdCategory.Low,
        VpdCategory.Optimal => ProtoVpdCategory.Optimal,
        VpdCategory.High => ProtoVpdCategory.High,
        VpdCategory.Critical => ProtoVpdCategory.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
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

    public static ProtoAlertSeverity ToProto(this DomainAlertSeverity severity) => severity switch
    {
        DomainAlertSeverity.None => ProtoAlertSeverity.None,
        DomainAlertSeverity.Yellow => ProtoAlertSeverity.Yellow,
        DomainAlertSeverity.Orange => ProtoAlertSeverity.Orange,
        DomainAlertSeverity.Red => ProtoAlertSeverity.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };
}
