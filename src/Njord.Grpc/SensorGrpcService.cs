using Akka.Actor;
using Akka.Hosting;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Sensors;
using DomainSensorKind = Njord.Sensors.SensorKind;
using ProtoSensorKind = Njord.Grpc.V2.SensorKind;

namespace Njord.Grpc;

public sealed class SensorGrpcService(
    ActorRegistry actorRegistry,
    IOptions<NjordOptions> njordOptions,
    TimeProvider timeProvider) : SensorService.SensorServiceBase
{
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

        var sensorHub = await actorRegistry.GetAsync<ISensorHub>(context.CancellationToken);
        var result = await sensorHub.Ask<PushResult>(new UpdateReading(reading!), TimeSpan.FromSeconds(5));
        return new PushResponse { Accepted = result.Accepted, RejectionReason = result.RejectionReason ?? "" };
    }

    public override async Task<PushResponse> StreamPush(
        IAsyncStreamReader<V2.SensorReading> requestStream,
        ServerCallContext context)
    {
        var sensorHub = await actorRegistry.GetAsync<ISensorHub>(context.CancellationToken);
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

            var result = await sensorHub.Ask<PushResult>(new UpdateReading(reading!), TimeSpan.FromSeconds(5));
            if (!result.Accepted)
            {
                rejected++;
            }
        }

        return rejected == 0
            ? new PushResponse { Accepted = true }
            : new PushResponse { Accepted = false, RejectionReason = $"{rejected}/{total} readings rejected" };
    }

    private (Njord.Sensors.SensorReading? Reading, string? Error) MapReading(V2.SensorReading request)
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
        var measuredAt = request.MeasuredAt?.ToDateTimeOffset() ?? timeProvider.GetUtcNow();

        var reading = new Njord.Sensors.SensorReading(kind, request.Location, source, request.Value, measuredAt);
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
