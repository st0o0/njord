using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Forecast;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using ActorSystem = Akka.Actor.ActorSystem;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class WeatherGrpcService(
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
    ServiceInfo serviceInfo,
    TimeProvider timeProvider) : WeatherService.WeatherServiceBase
{
    public override async Task<GetCatalogResponse> GetCatalog(GetCatalogRequest request, ServerCallContext context)
    {
        var catalog = serviceInfo.Catalog;
        return catalog.ToProto();
    }

    public override async Task<DomainDataUpdate> GetDomainData(GetDomainDataRequest request, ServerCallContext context)
    {
        var location = FindLocation(serviceInfo.Catalog, request.Location);
        if (request.Domain == Domain.Unspecified)
        {
            throw new RpcException(new GrpcStatus(StatusCode.InvalidArgument, "Domain must be specified"));
        }

        var domain = request.Domain.ToDomain();
        var result = await actorRegistry.AskAsync<IGrpcConsumer, DomainDataResponse>(
            new QueryDomainData(location.Location, domain), context);

        return result switch
        {
            DomainDataResult found when found.Event.ToDomainDataUpdate() is { } update => update,
            _ => throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No {request.Domain} data available yet for '{location.Location.Name}'")),
        };
    }

    public override async Task<GetEnrichmentsResponse> GetEnrichments(GetEnrichmentsRequest request, ServerCallContext context)
    {
        var location = FindLocation(serviceInfo.Catalog, request.Location);

        var result = await actorRegistry.AskAsync<IGrpcConsumer, LatestAnalysisResponse>(
            new QueryLatestAnalysis(location.Location), context);

        return result.ToProto();
    }

    public override async Task StreamDomainData(
        StreamDomainDataRequest request,
        IServerStreamWriter<DomainDataUpdate> responseStream,
        ServerCallContext context)
    {
        var location = ResolveStreamLocation(request.Location);
        var domains = request.Domains
            .Where(d => d != Domain.Unspecified)
            .Select(d => d.ToDomain())
            .ToHashSet();
        var sourceRef = await RequestGrpcStreamAsync(context);

        var mat = actorSystem.Materializer();
        var killSwitch = KillSwitches.Shared($"grpc-domain-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(killSwitch.Shutdown);

        await sourceRef.Source
            .Where(e => e.ToDomainKind() is { } kind
                && (domains.Count == 0 || domains.Contains(kind))
                && (location is not { } wanted || e.ToDomainLocation() == wanted))
            .Via(killSwitch.Flow<PipelineEvent>())
            .SelectAsync(1, async e =>
            {
                var update = e.ToDomainDataUpdate();
                if (update is not null)
                {
                    await responseStream.WriteAsync(update, context.CancellationToken);
                }

                return update;
            })
            .RunWith(Sink.Ignore<DomainDataUpdate?>(), mat);
    }

    public override async Task StreamEnrichments(
        StreamEnrichmentsRequest request,
        IServerStreamWriter<EnrichmentEvent> responseStream,
        ServerCallContext context)
    {
        var location = ResolveStreamLocation(request.Location);
        var sourceRef = await RequestGrpcStreamAsync(context);

        var mat = actorSystem.Materializer();
        var killSwitch = KillSwitches.Shared($"grpc-enrichment-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(killSwitch.Shutdown);

        await sourceRef.Source
            .Where(e => e.ToEnrichmentLocation() is { } eventLocation && (location is not { } wanted || eventLocation == wanted))
            .Via(killSwitch.Flow<PipelineEvent>())
            .SelectAsync(1, async update =>
            {
                var evt = update.ToEnrichmentEvent(timeProvider.GetUtcNow());
                if (evt is not null)
                {
                    await responseStream.WriteAsync(evt, context.CancellationToken);
                }

                return evt;
            })
            .RunWith(Sink.Ignore<EnrichmentEvent?>(), mat);
    }

    // An empty location streams every location; a non-empty one must be configured.
    private Location? ResolveStreamLocation(string location) =>
        string.IsNullOrEmpty(location) ? null : FindLocation(serviceInfo.Catalog, location).Location;

    private async Task<ISourceRef<PipelineEvent>> RequestGrpcStreamAsync(ServerCallContext context)
    {
        var reply = await actorRegistry.AskAsync<IGrpcConsumer, RequestGrpcStreamResponse>(
            new RequestGrpcStream(0), context);

        return reply switch
        {
            RequestGrpcStreamCompleted response => response.SourceRef,
            _ => throw new RpcException(
                new GrpcStatus(StatusCode.Unavailable, "Pipeline stream is currently unavailable")),
        };
    }

    private static CatalogLocation FindLocation(CatalogResult catalog, string name) =>
        catalog.Locations.FirstOrDefault(l => string.Equals(l.Location.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new RpcException(new GrpcStatus(StatusCode.NotFound, $"Location '{name}' not found"));
}
