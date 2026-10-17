using Njord.Grpc.V2;
using Njord.Messages.Config;
using Njord.Messages.Sensors;
using DomainSensorKind = Njord.Sensors.SensorKind;
using DomainSensorReading = Njord.Sensors.SensorReading;
using ProtoSensorKind = Njord.Grpc.V2.SensorKind;
using ProtoSensorReading = Njord.Grpc.V2.SensorReading;

namespace Njord.Grpc.Mapping;

internal static class SensorProtoMappings
{
    internal const string DefaultSource = "default";

    // The command for a valid reading, or the rejection reason: the kind is checked before the location.
    public static (UpdateReading? Command, string? Rejection) ToCommand(
        this ProtoSensorReading request, CatalogResult catalog, DateTimeOffset now)
    {
        if (!TryMapKind(request.Kind, out var kind))
        {
            return (null, $"Unknown or unspecified SensorKind: {request.Kind}");
        }

        // Forward the configured spelling: the SensorHub matches locations ordinally against Forecast.Location.
        var location = catalog.Locations.FirstOrDefault(
            l => string.Equals(l.Id.Name, request.Location, StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            return (null, $"Unknown location: {request.Location}");
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? DefaultSource : request.Source;
        var measuredAt = request.MeasuredAt?.ToDateTimeOffset() ?? now;

        return (new UpdateReading(new DomainSensorReading(kind, location.Id.Name, source, request.Value, measuredAt)), null);
    }

    public static PushResponse ToProto(this PushResult result) =>
        new() { Accepted = result.Accepted, RejectionReason = result.RejectionReason ?? "" };

    private static bool TryMapKind(ProtoSensorKind protoKind, out DomainSensorKind kind)
    {
        kind = protoKind switch
        {
            ProtoSensorKind.IndoorTemperature => DomainSensorKind.IndoorTemperature,
            ProtoSensorKind.IndoorHumidity => DomainSensorKind.IndoorHumidity,
            _ => default,
        };
        return protoKind != ProtoSensorKind.Unspecified;
    }
}
