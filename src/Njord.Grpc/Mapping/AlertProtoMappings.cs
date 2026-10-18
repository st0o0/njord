using Njord.Compute.Contracts;
using Njord.Grpc.V2;

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
}
