using Njord.Grpc.V2;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class AlertProtoMappings
{
    public static V2.Alert ToProto(this AlertResult result)
    {
        var alert = result.Alert;
        var protoAlert = new V2.Alert
        {
            Key = result.WireKey,
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

        return protoAlert;
    }

    public static AlertUpdate ToProto(this IEnumerable<AlertResult> results)
    {
        var update = new AlertUpdate();
        update.Alerts.AddRange(results.Select(result => result.ToProto()));
        return update;
    }
}
