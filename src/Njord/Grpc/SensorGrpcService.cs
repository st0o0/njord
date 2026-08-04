using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Sensors;
using Njord.Grpc.V2;
using Njord.Sensors;
using DomainSensorKind = Njord.Domain.Sensors.SensorKind;
using ProtoSensorKind = Njord.Grpc.V2.SensorKind;

namespace Njord.Grpc;

public sealed class SensorGrpcService(
    ActorRegistry actorRegistry,
    IOptions<NjordOptions> njordOptions) : V2.SensorService.SensorServiceBase
{
    private readonly IActorRef _sensorHub = actorRegistry.Get<SensorHubActor>();
    private readonly HashSet<string> _knownLocations = new(
        njordOptions.Value.Locations.Select(l => l.Name),
        StringComparer.OrdinalIgnoreCase);

    public override async Task<PushResponse> Push(V2.SensorReading request, ServerCallContext context)
    {
        var (reading, error) = MapReading(request);
        if (error is not null)
        {
            return new PushResponse { Accepted = false, RejectionReason = error };
        }

        var result = await _sensorHub.Ask<PushResult>(new UpdateReading(reading!), TimeSpan.FromSeconds(5));
        return new PushResponse { Accepted = result.Accepted, RejectionReason = result.RejectionReason ?? "" };
    }

    public override async Task<PushResponse> StreamPush(
        IAsyncStreamReader<V2.SensorReading> requestStream,
        ServerCallContext context)
    {
        var rejected = 0;
        var total = 0;

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            total++;
            var (reading, error) = MapReading(request);
            if (error is not null)
            {
                rejected++;
                continue;
            }

            var result = await _sensorHub.Ask<PushResult>(new UpdateReading(reading!), TimeSpan.FromSeconds(5));
            if (!result.Accepted)
            {
                rejected++;
            }
        }

        return rejected == 0
            ? new PushResponse { Accepted = true }
            : new PushResponse { Accepted = false, RejectionReason = $"{rejected}/{total} readings rejected" };
    }

    private (Domain.Sensors.SensorReading? Reading, string? Error) MapReading(V2.SensorReading request)
    {
        if (!TryMapKind(request.Kind, out var kind))
        {
            return (null, $"Unknown or unspecified SensorKind: {request.Kind}");
        }

        if (!_knownLocations.Contains(request.Location))
        {
            return (null, $"Unknown location: {request.Location}");
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? "default" : request.Source;
        var measuredAt = request.MeasuredAt?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow;

        var reading = new Domain.Sensors.SensorReading(kind, request.Location, source, request.Value, measuredAt);
        return (reading, null);
    }

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
