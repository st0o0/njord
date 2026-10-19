using Akka.Hosting;
using Grpc.Core;
using Njord.Core.Actors;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using Njord.Messages.Sensors;
using SensorReading = Njord.Grpc.V2.SensorReading;

namespace Njord.Grpc;

public sealed class SensorGrpcService(
    ActorRegistry actorRegistry,
    TimeProvider timeProvider,
    ServiceInfo serviceInfo) : SensorService.SensorServiceBase
{
    public override async Task<PushResponse> Push(SensorReading request, ServerCallContext context)
    {
        var catalog = serviceInfo.Catalog;
        var (command, rejection) = request.ToCommand(catalog, timeProvider.GetUtcNow());
        if (command is null)
        {
            return new PushResponse { Accepted = false, RejectionReason = rejection };
        }

        var result = await actorRegistry.AskAsync<ISensorHub, PushResult>(command, context);
        return result.ToProto();
    }

    public override async Task<PushResponse> StreamPush(
        IAsyncStreamReader<SensorReading> requestStream,
        ServerCallContext context)
    {
        var catalog = serviceInfo.Catalog;
        var rejected = 0;
        var total = 0;

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            total++;
            var (command, _) = request.ToCommand(catalog, timeProvider.GetUtcNow());
            if (command is null)
            {
                rejected++;
                continue;
            }

            var result = await actorRegistry.AskAsync<ISensorHub, PushResult>(command, context);
            if (!result.Accepted)
            {
                rejected++;
            }
        }

        return rejected == 0
            ? new PushResponse { Accepted = true }
            : new PushResponse { Accepted = false, RejectionReason = $"{rejected}/{total} readings rejected" };
    }
}
