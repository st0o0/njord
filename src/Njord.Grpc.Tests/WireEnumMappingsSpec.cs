using Njord.Compute.Contracts;
using Njord.Grpc.Mapping;
using DomainAlertSeverity = Njord.Compute.Contracts.AlertSeverity;
using DomainAlertType = Njord.Compute.Contracts.AlertType;
using DomainConsensusMethod = Njord.Compute.Settings.ConsensusMethod;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoAlertType = Njord.Grpc.V2.AlertType;
using ProtoConsensusMethod = Njord.Grpc.V2.ConsensusMethod;
using ProtoDewPointComfort = Njord.Grpc.V2.DewPointComfort;
using ProtoPollPhase = Njord.Grpc.V2.PollPhase;
using ProtoStability = Njord.Grpc.V2.Stability;
using ProtoTrendDirection = Njord.Grpc.V2.TrendDirection;
using ProtoVpdCategory = Njord.Grpc.V2.VpdCategory;

namespace Njord.Grpc.Tests;

public sealed class WireEnumMappingsSpec
{
    [Theory]
    [InlineData(DomainPollPhase.Discovery, ProtoPollPhase.Discovery)]
    [InlineData(DomainPollPhase.Steady, ProtoPollPhase.Steady)]
    public void Poll_phase_maps_to_its_proto_member(DomainPollPhase phase, ProtoPollPhase expected) =>
        Assert.Equal(expected, phase.ToProto());

    [Theory]
    [InlineData(TrendDirection.Rising, ProtoTrendDirection.Rising)]
    [InlineData(TrendDirection.Falling, ProtoTrendDirection.Falling)]
    [InlineData(TrendDirection.Stable, ProtoTrendDirection.Stable)]
    public void Trend_direction_maps_to_its_proto_member(TrendDirection direction, ProtoTrendDirection expected) =>
        Assert.Equal(expected, direction.ToProto());

    [Theory]
    [InlineData(Stability.Converging, ProtoStability.Converging)]
    [InlineData(Stability.Diverging, ProtoStability.Diverging)]
    [InlineData(Stability.Stable, ProtoStability.Stable)]
    public void Stability_maps_to_its_proto_member(Stability stability, ProtoStability expected) =>
        Assert.Equal(expected, stability.ToProto());

    [Theory]
    [InlineData(DewPointComfort.Dry, ProtoDewPointComfort.Dry)]
    [InlineData(DewPointComfort.Comfortable, ProtoDewPointComfort.Comfortable)]
    [InlineData(DewPointComfort.Sticky, ProtoDewPointComfort.Sticky)]
    [InlineData(DewPointComfort.Oppressive, ProtoDewPointComfort.Oppressive)]
    [InlineData(DewPointComfort.Dangerous, ProtoDewPointComfort.Dangerous)]
    public void Dew_point_comfort_maps_to_its_proto_member(DewPointComfort comfort, ProtoDewPointComfort expected) =>
        Assert.Equal(expected, comfort.ToProto());

    [Theory]
    [InlineData(VpdCategory.Low, ProtoVpdCategory.Low)]
    [InlineData(VpdCategory.Optimal, ProtoVpdCategory.Optimal)]
    [InlineData(VpdCategory.High, ProtoVpdCategory.High)]
    [InlineData(VpdCategory.Critical, ProtoVpdCategory.Critical)]
    public void Vpd_category_maps_to_its_proto_member(VpdCategory category, ProtoVpdCategory expected) =>
        Assert.Equal(expected, category.ToProto());

    [Theory]
    [InlineData(DomainConsensusMethod.Mean, ProtoConsensusMethod.Mean)]
    [InlineData(DomainConsensusMethod.Median, ProtoConsensusMethod.Median)]
    [InlineData(DomainConsensusMethod.TrimmedMean, ProtoConsensusMethod.TrimmedMean)]
    public void Consensus_method_maps_both_ways(DomainConsensusMethod method, ProtoConsensusMethod expected)
    {
        Assert.Equal(expected, method.ToProto());
        Assert.Equal(method, expected.ToDomain());
    }

    [Fact]
    public void Unspecified_consensus_method_means_unchanged() =>
        Assert.Null(ProtoConsensusMethod.Unspecified.ToDomain());

    [Theory]
    [InlineData(DomainAlertType.Frost, ProtoAlertType.Frost)]
    [InlineData(DomainAlertType.Heat, ProtoAlertType.Heat)]
    [InlineData(DomainAlertType.Storm, ProtoAlertType.Storm)]
    [InlineData(DomainAlertType.HeavyRain, ProtoAlertType.HeavyRain)]
    [InlineData(DomainAlertType.Uv, ProtoAlertType.Uv)]
    [InlineData(DomainAlertType.Fog, ProtoAlertType.Fog)]
    [InlineData(DomainAlertType.Snow, ProtoAlertType.Snow)]
    [InlineData(DomainAlertType.PressureDrop, ProtoAlertType.PressureDrop)]
    [InlineData(DomainAlertType.Thunderstorm, ProtoAlertType.Thunderstorm)]
    [InlineData(DomainAlertType.Ice, ProtoAlertType.Ice)]
    [InlineData(DomainAlertType.WindChill, ProtoAlertType.WindChill)]
    [InlineData(DomainAlertType.Visibility, ProtoAlertType.Visibility)]
    [InlineData(DomainAlertType.TropicalNight, ProtoAlertType.TropicalNight)]
    [InlineData(DomainAlertType.Humidity, ProtoAlertType.Humidity)]
    public void Alert_type_maps_to_its_proto_member(DomainAlertType type, ProtoAlertType expected) =>
        Assert.Equal(expected, type.ToProto());

    [Theory]
    [InlineData(DomainAlertSeverity.None, ProtoAlertSeverity.None)]
    [InlineData(DomainAlertSeverity.Yellow, ProtoAlertSeverity.Yellow)]
    [InlineData(DomainAlertSeverity.Orange, ProtoAlertSeverity.Orange)]
    [InlineData(DomainAlertSeverity.Red, ProtoAlertSeverity.Red)]
    public void Alert_severity_maps_to_its_proto_member(DomainAlertSeverity severity, ProtoAlertSeverity expected) =>
        Assert.Equal(expected, severity.ToProto());

    [Fact]
    public void Every_alert_type_has_a_mapping()
    {
        Assert.All(Enum.GetValues<DomainAlertType>(), v => Assert.NotEqual(ProtoAlertType.Unspecified, v.ToProto()));
    }

    [Fact]
    public void Undefined_alert_values_throw()
    {
        Assert.Multiple(
            () => Assert.Equal("type", Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainAlertType)99).ToProto()).ParamName),
            () => Assert.Equal("severity", Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainAlertSeverity)99).ToProto()).ParamName));
    }

    [Fact]
    public void Every_domain_member_has_a_proto_mapping()
    {
        Assert.All(Enum.GetValues<DomainPollPhase>(), v => Assert.NotEqual(ProtoPollPhase.Unspecified, v.ToProto()));
        Assert.All(Enum.GetValues<TrendDirection>(), v => Assert.NotEqual(ProtoTrendDirection.Unspecified, v.ToProto()));
        Assert.All(Enum.GetValues<Stability>(), v => Assert.NotEqual(ProtoStability.Unspecified, v.ToProto()));
        Assert.All(Enum.GetValues<DewPointComfort>(), v => Assert.NotEqual(ProtoDewPointComfort.Unspecified, v.ToProto()));
        Assert.All(Enum.GetValues<VpdCategory>(), v => Assert.NotEqual(ProtoVpdCategory.Unspecified, v.ToProto()));
        Assert.All(Enum.GetValues<DomainConsensusMethod>(), v => Assert.NotEqual(ProtoConsensusMethod.Unspecified, v.ToProto()));
    }

    [Fact]
    public void Undefined_domain_values_throw()
    {
        Assert.Multiple(
            () => Assert.Equal("phase", Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainPollPhase)99).ToProto()).ParamName),
            () => Assert.Equal("direction", Assert.Throws<ArgumentOutOfRangeException>(() => ((TrendDirection)99).ToProto()).ParamName),
            () => Assert.Equal("stability", Assert.Throws<ArgumentOutOfRangeException>(() => ((Stability)99).ToProto()).ParamName),
            () => Assert.Equal("comfort", Assert.Throws<ArgumentOutOfRangeException>(() => ((DewPointComfort)99).ToProto()).ParamName),
            () => Assert.Equal("category", Assert.Throws<ArgumentOutOfRangeException>(() => ((VpdCategory)99).ToProto()).ParamName),
            () => Assert.Equal("method", Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainConsensusMethod)99).ToProto()).ParamName));
    }

    [Fact]
    public void Undefined_proto_consensus_method_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((ProtoConsensusMethod)42).ToDomain());

        Assert.Equal("method", ex.ParamName);
    }
}
