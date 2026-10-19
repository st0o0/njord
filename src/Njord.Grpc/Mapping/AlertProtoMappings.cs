using Njord.Grpc.V2;
using Njord.Messages.Alerts;

namespace Njord.Grpc.Mapping;

internal static class AlertProtoMappings
{
    public static AlertUpdate ToProto(this AlertResult result)
    {
        var update = new AlertUpdate();
        foreach (var alert in result.Alerts)
        {
            var protoAlert = new V2.Alert
            {
                Type = alert.Type.ToProto(),
                Severity = alert.Severity.ToProto(),
                Confidence = alert.Confidence,
                TriggerValue = alert.TriggerValue,
                Threshold = alert.Threshold,
            };
            if (alert.PeakValue is { } pv)
            {
                protoAlert.PeakValue = pv;
            }

            if (alert.HoursUntil is { } hu)
            {
                protoAlert.HoursUntil = hu;
            }

            if (alert.DurationHours is { } dh)
            {
                protoAlert.DurationHours = dh;
            }

            update.Alerts.Add(protoAlert);
        }

        return update;
    }

    public static AlertUpdate ToProto(this AirQualityAlert alert) => new()
    {
        Alerts = { alert.Type.ToProto().ToProtoAlert(alert.Severity, alert.TriggerValue, alert.Threshold, alert.HoursUntil) },
    };

    public static AlertUpdate ToProto(this FloodAlert alert) => new()
    {
        Alerts = { alert.Type.ToProto().ToProtoAlert(alert.Severity, alert.TriggerValue, alert.Threshold, alert.HoursUntil) },
    };

    private static V2.Alert ToProtoAlert(
        this V2.AlertType type, Messages.Alerts.AlertSeverity severity, double triggerValue, double threshold, int? hoursUntil)
    {
        var protoAlert = new V2.Alert
        {
            Type = type,
            Severity = severity.ToProto(),
            Confidence = 1.0,
            TriggerValue = triggerValue,
            Threshold = threshold,
        };
        if (hoursUntil is { } hu)
        {
            protoAlert.HoursUntil = hu;
        }

        return protoAlert;
    }
}
