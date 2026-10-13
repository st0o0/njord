using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Njord.Core.Actors;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using ActorSystem = Akka.Actor.ActorSystem;
using ConfigResponse = Njord.Grpc.V2.ConfigResponse;
using QueryConfigResponse = Njord.Messages.Config.ConfigResponse;

namespace Njord.Grpc;

public sealed class AdminGrpcService(
    ActorRegistry actorRegistry,
    ActorSystem actorSystem) : AdminService.AdminServiceBase
{
    internal const string RestartNoticePrefix = ConfigProtoMappings.RestartNoticePrefix;

    public override async Task<NjordConfig> GetConfig(GetConfigRequest request, ServerCallContext context)
    {
        var response = await actorRegistry.AskAsync<IConfigManager, QueryConfigResponse>(new QueryConfig(), context);
        return response.ToProto();
    }

    // Sends the current configuration, then every changed one, until the client disconnects.
    public override async Task StreamConfig(
        StreamConfigRequest request,
        IServerStreamWriter<NjordConfig> responseStream,
        ServerCallContext context)
    {
        var reply = await actorRegistry.AskAsync<IConfigManager, RequestConfigStreamResponse>(
            new RequestConfigStream(0), context);
        var sourceRef = reply switch
        {
            RequestConfigStreamCompleted completed => completed.SourceRef,
            _ => throw new RpcException(new Status(StatusCode.Unavailable, "Config stream is currently unavailable")),
        };

        var killSwitch = KillSwitches.Shared($"grpc-config-{context.Peer}");
        await using var cancellation = context.CancellationToken.Register(() => killSwitch.Shutdown());

        NjordConfig? last = null;
        await sourceRef.Source
            .Via(killSwitch.Flow<ConfigSnapshot>())
            .SelectAsync(1, async snapshot =>
            {
                var current = snapshot.ToProto();
                if (!current.Equals(last))
                {
                    last = current;
                    await responseStream.WriteAsync(current);
                }

                return current;
            })
            .RunWith(Sink.Ignore<NjordConfig>(), actorSystem.Materializer());
    }

    public override async Task<ConfigResponse> SetLocations(SetLocationsRequest request, ServerCallContext context)
    {
        var response = await actorRegistry.AskAsync<IConfigManager, UpdateLocationsResponse>(request.ToCommand(), context);
        return response.ToProto();
    }

    public override async Task<ConfigResponse> SetSettings(SetSettingsRequest request, ServerCallContext context)
    {
        var response = await actorRegistry.AskAsync<IConfigManager, UpdateSettingsResponse>(request.ToCommand(), context);
        return response.ToProto();
    }

    public override async Task<ConfigResponse> SetEnrichment(SetEnrichmentRequest request, ServerCallContext context)
    {
        var response = await actorRegistry.AskAsync<IConfigManager, UpdateAnalysisConfigResponse>(request.ToCommand(), context);
        return response.ToProto();
    }

    public override async Task<ConfigResponse> SetBudget(SetBudgetRequest request, ServerCallContext context)
    {
        var response = await actorRegistry.AskAsync<IConfigManager, UpdateBudgetResponse>(request.ToCommand(), context);
        return response.ToProto();
    }
}
