using Njord.Grpc.Mapping;
using Njord.Model;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainPollPhase = Njord.Messages.Pipeline.PollPhase;
using ProtoAlertSeverity = Njord.Grpc.V2.AlertSeverity;
using ProtoAlertType = Njord.Grpc.V2.AlertType;
using ProtoDomain = Njord.Grpc.V2.Domain;
using ProtoPollPhase = Njord.Grpc.V2.PollPhase;

namespace Njord.Grpc.Tests;

public sealed class WireEnumMappingsSpec
{
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
    }

    [Fact]
    public void Undefined_domain_values_throw()
    {
        Assert.Multiple(
            () => Assert.Equal("phase", Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainPollPhase)99).ToProto()).ParamName));
    }

    [Theory]
    [InlineData(DomainKind.Weather, ProtoDomain.Weather)]
    [InlineData(DomainKind.AirQuality, ProtoDomain.AirQuality)]
    [InlineData(DomainKind.Flood, ProtoDomain.Flood)]
    [InlineData(DomainKind.Solar, ProtoDomain.Solar)]
    [InlineData(DomainKind.Marine, ProtoDomain.Marine)]
    public void Domain_maps_to_its_proto_member_and_back(DomainKind domain, ProtoDomain expected)
    {
        Assert.Multiple(
            () => Assert.Equal(expected, domain.ToProto()),
            () => Assert.Equal(domain, expected.ToDomain()));
    }

    [Fact]
    public void Every_domain_kind_has_a_proto_mapping()
    {
        Assert.All(Enum.GetValues<DomainKind>(), v => Assert.NotEqual(ProtoDomain.Unspecified, v.ToProto()));
    }

    [Fact]
    public void Undefined_and_unspecified_domains_throw()
    {
        Assert.Multiple(
            () => Assert.Equal("domain", Assert.Throws<ArgumentOutOfRangeException>(() => ((DomainKind)99).ToProto()).ParamName),
            () => Assert.Equal("domain", Assert.Throws<ArgumentOutOfRangeException>(() => ProtoDomain.Unspecified.ToDomain()).ParamName));
    }
}
