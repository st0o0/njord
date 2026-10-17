using Njord.Compute.Contracts;
using DomainConsensusMethod = Njord.Compute.Settings.ConsensusMethod;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoConsensusMethod = Njord.Grpc.V2.ConsensusMethod;
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

    public static ProtoConsensusMethod ToProto(this DomainConsensusMethod method) => method switch
    {
        DomainConsensusMethod.Mean => ProtoConsensusMethod.Mean,
        DomainConsensusMethod.Median => ProtoConsensusMethod.Median,
        DomainConsensusMethod.TrimmedMean => ProtoConsensusMethod.TrimmedMean,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    public static DomainConsensusMethod? ToDomain(this ProtoConsensusMethod method) => method switch
    {
        ProtoConsensusMethod.Unspecified => null,
        ProtoConsensusMethod.Mean => DomainConsensusMethod.Mean,
        ProtoConsensusMethod.Median => DomainConsensusMethod.Median,
        ProtoConsensusMethod.TrimmedMean => DomainConsensusMethod.TrimmedMean,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };
}
