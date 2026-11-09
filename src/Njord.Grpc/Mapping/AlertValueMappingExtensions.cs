using Njord.Messages.Results;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainConfidenceLevel = Njord.Messages.Alerts.ConfidenceLevel;
using ProtoAlertSeverity = Njord.Grpc.Proto.AlertSeverity;
using ProtoConfidenceLevel = Njord.Grpc.Proto.ConfidenceLevel;

namespace Njord.Grpc.Mapping;

internal static class AlertValueMappingExtensions
{
    public static Proto.AlertValue ToAlertValue(this AlertFeatureResult r)
    {
        var proto = new Proto.AlertValue
        {
            Severity = ToProtoSeverity(r.Severity),
            Confidence = r.Confidence,
            TriggerValue = r.TriggerValue,
            Threshold = r.Threshold,
            ConfidenceLevel = ToProtoConfidence(r.Level),
        };
        if (r.PeakValue is { } peakValue)
        {
            proto.PeakValue = peakValue;
        }

        if (r.HoursUntil is { } hoursUntil)
        {
            proto.HoursUntil = hoursUntil;
        }

        if (r.DurationHours is { } durationHours)
        {
            proto.DurationHours = durationHours;
        }

        return proto;
    }

    private static ProtoAlertSeverity ToProtoSeverity(DomainAlertSeverity severity) => severity switch
    {
        DomainAlertSeverity.None => ProtoAlertSeverity.None,
        DomainAlertSeverity.Yellow => ProtoAlertSeverity.Yellow,
        DomainAlertSeverity.Orange => ProtoAlertSeverity.Orange,
        DomainAlertSeverity.Red => ProtoAlertSeverity.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    private static ProtoConfidenceLevel ToProtoConfidence(DomainConfidenceLevel level) => level switch
    {
        DomainConfidenceLevel.Low => ProtoConfidenceLevel.Low,
        DomainConfidenceLevel.Medium => ProtoConfidenceLevel.Medium,
        DomainConfidenceLevel.High => ProtoConfidenceLevel.High,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
