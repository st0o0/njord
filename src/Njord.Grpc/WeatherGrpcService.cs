using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Njord.Core.Actors;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Weather;
using ActorSystem = Akka.Actor.ActorSystem;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class WeatherGrpcService(
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
    TimeProvider timeProvider) : WeatherService.WeatherServiceBase
{
    public override async Task<GetCatalogResponse> GetCatalog(GetCatalogRequest request, ServerCallContext context)
    {
        var catalog = await AskCatalogAsync(context);
        return catalog.ToProto();
    }

    public override async Task<GetForecastResponse> GetForecast(GetForecastRequest request, ServerCallContext context)
    {
        var location = FindLocation(await AskCatalogAsync(context), request.Location);
        ValidateModel(location, request.Model);

        var result = await actorRegistry.AskAsync<IForecastReadModel, ForecastResponse>(
            new QueryForecast(new LocationId(request.Location), request.Model), context);

        return result switch
        {
            ForecastResult found => found.ToProto(timeProvider.GetUtcNow()),
            _ => throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No forecast data available yet for '{request.Location}/{request.Model}'")),
        };
    }

    public override async Task<GetEnrichmentsResponse> GetEnrichments(GetEnrichmentsRequest request, ServerCallContext context)
    {
        FindLocation(await AskCatalogAsync(context), request.Location);

        var result = await actorRegistry.AskAsync<IForecastReadModel, LatestAnalysisResponse>(
            new QueryLatestAnalysis(new LocationId(request.Location)), context);

        return result.ToProto();
    }

    public override async Task StreamForecasts(
        StreamForecastsRequest request,
        IServerStreamWriter<ForecastUpdate> responseStream,
        ServerCallContext context)
    {
        var sourceRef = await RequestReadModelStreamAsync(context);

        var mat = actorSystem.Materializer();
        var killSwitch = KillSwitches.Shared($"grpc-forecast-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(() => killSwitch.Shutdown());

        await sourceRef.Source
            .Collect(e => e is PipelineEvent.ForecastReceived, e => (PipelineEvent.ForecastReceived)e)
            .Where(u => string.IsNullOrEmpty(request.Location) ||
                        string.Equals(u.Location.Name, request.Location, StringComparison.OrdinalIgnoreCase))
            .Via(killSwitch.Flow<PipelineEvent.ForecastReceived>())
            .SelectAsync(1, async update =>
            {
                var proto = update.ToProto(timeProvider.GetUtcNow());
                await responseStream.WriteAsync(proto, context.CancellationToken);
                return proto;
            })
            .RunWith(Sink.Ignore<ForecastUpdate>(), mat);
    }

    public override async Task StreamEnrichments(
        StreamEnrichmentsRequest request,
        IServerStreamWriter<EnrichmentEvent> responseStream,
        ServerCallContext context)
    {
        var sourceRef = await RequestReadModelStreamAsync(context);

        var mat = actorSystem.Materializer();
        var killSwitch = KillSwitches.Shared($"grpc-enrichment-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(() => killSwitch.Shutdown());

        await sourceRef.Source
            .Where(e => e is PipelineEvent.ConsensusComputed or PipelineEvent.AlertComputed
                or PipelineEvent.DerivedComputed or PipelineEvent.TrendComputed
                or PipelineEvent.IndexComputed)
            .Via(killSwitch.Flow<PipelineEvent>())
            .SelectAsync(1, async update =>
            {
                var evt = update.ToEnrichmentEvent();
                if (evt is not null)
                    await responseStream.WriteAsync(evt, context.CancellationToken);
                return evt;
            })
            .RunWith(Sink.Ignore<EnrichmentEvent?>(), mat);
    }

    private async Task<CatalogResult> AskCatalogAsync(ServerCallContext context) =>
        (await actorRegistry.AskAsync<IConfigManager, CatalogResponse>(new QueryCatalog(), context)).ToResult();

    private async Task<ISourceRef<PipelineEvent>> RequestReadModelStreamAsync(ServerCallContext context)
    {
        var reply = await actorRegistry.AskAsync<IForecastReadModel, RequestReadModelStreamResponse>(
            new RequestReadModelStream(0), context);

        return reply switch
        {
            RequestReadModelStreamCompleted response => response.SourceRef,
            _ => throw new RpcException(
                new GrpcStatus(StatusCode.Unavailable, "Pipeline stream is currently unavailable")),
        };
    }

    private static CatalogLocation FindLocation(CatalogResult catalog, string name) =>
        catalog.Locations.FirstOrDefault(l => string.Equals(l.Id.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new RpcException(new GrpcStatus(StatusCode.NotFound, $"Location '{name}' not found"));

    private static void ValidateModel(CatalogLocation location, string modelId)
    {
        if (!location.Models.Contains(modelId, StringComparer.OrdinalIgnoreCase))
        {
            throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"Model '{modelId}' not configured for '{location.Id.Name}'"));
        }
    }
}
