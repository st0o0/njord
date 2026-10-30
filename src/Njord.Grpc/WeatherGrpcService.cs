using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Config;
using Njord.Messages.Fetch;
using Njord.Messages.Stream;
using ActorSystem = Akka.Actor.ActorSystem;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class WeatherGrpcService(
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
    IOptions<NjordOptions> scope,
    IOptions<ActiveModels> models,
    TimeProvider timeProvider) : WeatherService.WeatherServiceBase
{
    private readonly CatalogResult _catalog = scope.Value.ToCatalog(models.Value);

    public override async Task<GetCatalogResponse> GetCatalog(GetCatalogRequest request, ServerCallContext context)
    {
        return _catalog.ToProto();
    }

    public override async Task<DomainDataUpdate> GetDomainData(GetDomainDataRequest request, ServerCallContext context)
    {
        var location = FindLocation(_catalog, request.Location);
        if (request.Domain == Domain.Unspecified)
        {
            throw new RpcException(new GrpcStatus(StatusCode.InvalidArgument, "Domain must be specified"));
        }

        var domain = request.Domain.ToDomain();
        var primary = location.Models.FirstOrDefault() ?? string.Empty;
        var modelId = ResolveModel(request, domain, location, primary);
        var result = await actorRegistry.AskAsync<IGrpcConsumer, DomainDataResponse>(
            new QueryDomainData(location.Location, domain, modelId), context);

        return result switch
        {
            DomainDataResult found when found.Event.ToDomainDataUpdate() is { } update => WithPrimaryModel(update, primary),
            _ => throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"No {request.Domain} data available yet for '{location.Location.Name}'")),
        };
    }

    public override async Task<GetEnrichmentsResponse> GetEnrichments(GetEnrichmentsRequest request, ServerCallContext context)
    {
        var location = FindLocation(_catalog, request.Location);

        var result = await actorRegistry.AskAsync<IGrpcConsumer, LatestAnalysisResponse>(
            new QueryLatestAnalysis(location.Location), context);

        return result.ToProto(timeProvider.GetUtcNow());
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
                var update = e.ToDomainDataUpdate() is { } mapped
                    ? WithPrimaryModel(mapped, _catalog.Locations.FirstOrDefault()?.Models.FirstOrDefault() ?? string.Empty)
                    : null;
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

    private static string? ResolveModel(GetDomainDataRequest request, DomainKind domain, CatalogLocation location, string primary)
    {
        if (string.IsNullOrEmpty(request.Model) || (domain == DomainKind.Forecast && request.Model == primary))
        {
            return null;
        }

        if (domain != DomainKind.Forecast || !location.Models.Contains(request.Model))
        {
            throw new RpcException(new GrpcStatus(StatusCode.NotFound,
                $"Model '{request.Model}' is not enabled for '{location.Location.Name}'"));
        }

        return request.Model;
    }

    private static DomainDataUpdate WithPrimaryModel(DomainDataUpdate update, string primary)
    {
        if (update.DataCase == DomainDataUpdate.DataOneofCase.Forecast && update.Model.Length == 0)
        {
            update.Model = primary;
        }

        return update;
    }

    // An empty location streams every location; a non-empty one must be configured.
    private Location? ResolveStreamLocation(string location) =>
        string.IsNullOrEmpty(location) ? null : FindLocation(_catalog, location).Location;

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
