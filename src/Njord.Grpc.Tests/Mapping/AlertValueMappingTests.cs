using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results.Alerts;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainConfidenceLevel = Njord.Messages.Alerts.ConfidenceLevel;
using ProtoAlertSeverity = Njord.Grpc.Proto.AlertSeverity;
using ProtoConfidenceLevel = Njord.Grpc.Proto.ConfidenceLevel;

namespace Njord.Grpc.Tests.Mapping;

public sealed class AlertValueMappingTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    [Theory]
    [InlineData(DomainAlertSeverity.None, ProtoAlertSeverity.None)]
    [InlineData(DomainAlertSeverity.Yellow, ProtoAlertSeverity.Yellow)]
    [InlineData(DomainAlertSeverity.Orange, ProtoAlertSeverity.Orange)]
    [InlineData(DomainAlertSeverity.Red, ProtoAlertSeverity.Red)]
    public void Severity_maps_to_proto(DomainAlertSeverity domain, ProtoAlertSeverity expected)
    {
        var result = new FrostAlertResult(_vienna, domain, 0.8, -2.0, 0.0, null, null, null, DomainConfidenceLevel.Medium, -3.0, null);

        Assert.Equal(expected, result.ToAlertValue().Severity);
    }

    [Theory]
    [InlineData(DomainConfidenceLevel.Low, ProtoConfidenceLevel.Low)]
    [InlineData(DomainConfidenceLevel.Medium, ProtoConfidenceLevel.Medium)]
    [InlineData(DomainConfidenceLevel.High, ProtoConfidenceLevel.High)]
    public void Confidence_level_maps_to_proto(DomainConfidenceLevel domain, ProtoConfidenceLevel expected)
    {
        var result = new FrostAlertResult(_vienna, DomainAlertSeverity.Yellow, 0.8, -2.0, 0.0, null, null, null, domain, -3.0, null);

        Assert.Equal(expected, result.ToAlertValue().ConfidenceLevel);
    }

    [Fact]
    public void Numeric_fields_and_optionals_map()
    {
        var result = new FrostAlertResult(_vienna, DomainAlertSeverity.Red, 0.75, -2.5, 0.5, -4.0, 6, 3, DomainConfidenceLevel.High, -3.0, 5);

        var value = result.ToAlertValue();

        Assert.Multiple(
            () => Assert.Equal(0.75, value.Confidence),
            () => Assert.Equal(-2.5, value.TriggerValue),
            () => Assert.Equal(0.5, value.Threshold),
            () => Assert.Equal(-4.0, value.PeakValue),
            () => Assert.Equal(6, value.HoursUntil),
            () => Assert.Equal(3, value.DurationHours));
    }

    [Fact]
    public void Missing_optionals_stay_unset()
    {
        var result = new FrostAlertResult(_vienna, DomainAlertSeverity.Red, 0.75, -2.5, 0.5, null, null, null, DomainConfidenceLevel.High, -3.0, null);

        var value = result.ToAlertValue();

        Assert.Multiple(
            () => Assert.False(value.HasPeakValue),
            () => Assert.False(value.HasHoursUntil),
            () => Assert.False(value.HasDurationHours));
    }

    [Fact]
    public void Unknown_severity_throws()
    {
        var result = new FrostAlertResult(_vienna, (DomainAlertSeverity)99, 0.8, 0, 0, null, null, null, DomainConfidenceLevel.Low, 0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => result.ToAlertValue());
    }
}
