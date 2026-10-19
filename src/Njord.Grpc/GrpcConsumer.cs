using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc;

public sealed class GrpcConsumer : ReceiveActor
{
    private const int StreamClientBufferSize = 256;

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IMaterializer _mat = null!;

    private IActorRef _hubInput = ActorRefs.Nobody;
    private Source<PipelineEvent, NotUsed> _clientHubSource = Source.Empty<PipelineEvent>();

    private ImmutableDictionary<(LocationId Location, DomainKind Domain), PipelineEvent> _domainData =
        ImmutableDictionary<(LocationId, DomainKind), PipelineEvent>.Empty;

    private ImmutableDictionary<LocationId, PipelineEvent.AlertComputed> _alerts =
        ImmutableDictionary<LocationId, PipelineEvent.AlertComputed>.Empty;

    private sealed record PipelineResolved(IActorRef Pipeline);
    private sealed record PipelineResolveFailed(Exception Cause);
    private sealed record StreamCompleted;

    public GrpcConsumer()
    {
        Receive<QueryDomainData>(HandleQueryDomainData);
        Receive<QueryLatestAnalysis>(HandleQueryLatestAnalysis);
        Receive<RequestGrpcStream>(HandleStreamRequest);
        Receive<PipelineEvent>(OnPipelineEvent);
        Receive<StreamCompleted>(_ => { });
        Receive<PipelineResolved>(OnPipelineResolved);
        Receive<PipelineResolveFailed>(msg => _log.Warning(msg.Cause, "Failed to resolve pipeline — gRPC consumer has no data"));
        Receive<RequestPipelineOutputCompleted>(OnSourceRef);
        Receive<RequestPipelineOutputFailed>(msg => _log.Warning(msg.Cause, "Pipeline could not create a SourceRef"));
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        var (hubInput, hubSource) = Source.ActorRef<PipelineEvent>(256, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Keep.Both)
            .Run(_mat);

        hubSource.RunWith(Sink.Ignore<PipelineEvent>(), _mat);

        _hubInput = hubInput;
        _clientHubSource = hubSource;

        ActorRegistry.For(Context.System).GetAsync<IForecastPipeline>().PipeTo(Self,
            success: pipeline => new PipelineResolved(pipeline),
            failure: ex => new PipelineResolveFailed(ex));
    }

    private void OnPipelineResolved(PipelineResolved msg)
    {
        msg.Pipeline.Tell(new RequestPipelineOutput(0));
    }

    private void OnSourceRef(RequestPipelineOutputCompleted msg)
    {
        _log.Info("Pipeline SourceRef received — gRPC consumer attached");
        msg.SourceRef.Source
            .WatchTermination(Keep.Right)
            .To(Sink.ActorRef<PipelineEvent>(Self, new StreamCompleted(), _ => new StreamCompleted()))
            .Run(_mat);
    }

    private void OnPipelineEvent(PipelineEvent evt)
    {
        UpdateState(evt);
        _hubInput.Tell(evt);
    }

    private void HandleQueryDomainData(QueryDomainData msg)
    {
        Sender.Tell(_domainData.TryGetValue((msg.Location, msg.Domain), out var latest)
            ? new DomainDataResult(latest)
            : new DomainDataNotAvailable(msg.Location, msg.Domain));
    }

    private void HandleQueryLatestAnalysis(QueryLatestAnalysis msg)
    {
        Sender.Tell(_alerts.TryGetValue(msg.Location, out var alerts)
            ? new LatestAnalysisResult(msg.Location, alerts)
            : new LatestAnalysisNotAvailable(msg.Location));
    }

    private void HandleStreamRequest(RequestGrpcStream msg)
    {
        _clientHubSource
            .Buffer(StreamClientBufferSize, OverflowStrategy.DropHead)
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat)
            .PipeTo(Sender, Self,
                sr => new RequestGrpcStreamCompleted(msg.RequestId, sr),
                ex =>
                {
                    _log.Error(ex, "Failed to create gRPC stream SourceRef");
                    return new RequestGrpcStreamFailed(msg.RequestId, ex);
                });
    }

    private void UpdateState(PipelineEvent evt)
    {
        if (evt.ToDomainKind() is { } domain && evt.ToDomainLocation() is { } location)
        {
            _domainData = _domainData.SetItem((location, domain), evt);
            return;
        }

        switch (evt)
        {
            case PipelineEvent.AlertRaised raised:
                var raisedLocation = new LocationId(raised.Location.Name);
                _alerts = _alerts.SetItem(raisedLocation, MergeAlert(_alerts.GetValueOrDefault(raisedLocation), raised));
                break;
            case PipelineEvent.AlertComputed computed:
                _alerts = _alerts.SetItem(computed.Result.Location, computed);
                break;
        }
    }

    private static PipelineEvent.AlertComputed MergeAlert(PipelineEvent.AlertComputed? latest, PipelineEvent.AlertRaised raised)
    {
        var alerts = (latest?.Result.Alerts ?? [])
            .Where(alert => alert.Type != raised.Type)
            .Append(raised.Alert)
            .OrderBy(alert => alert.Type)
            .ToList();
        return new PipelineEvent.AlertComputed(
            new AlertResult(new LocationId(raised.Location.Name), alerts), raised.Timestamp);
    }
}
