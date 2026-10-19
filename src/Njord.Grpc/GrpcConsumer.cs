using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Servus.Akka;

namespace Njord.Grpc;

public sealed class GrpcConsumer : ReceiveActor
{
    private const int StreamClientBufferSize = 256;

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IMaterializer _mat = null!;

    private IActorRef _hubInput = ActorRefs.Nobody;
    private Source<PipelineEvent, NotUsed> _clientHubSource = Source.Empty<PipelineEvent>();

    private GrpcConsumerState _state = GrpcConsumerState.Empty;

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

        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
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
        _state = _state.Apply(evt);
        _hubInput.Tell(evt);
    }

    private void HandleQueryDomainData(QueryDomainData msg)
    {
        Sender.Tell(_state.GetDomainData(msg.Location, msg.Domain) is { } latest
            ? new DomainDataResult(latest)
            : new DomainDataNotAvailable(msg.Location, msg.Domain));
    }

    private void HandleQueryLatestAnalysis(QueryLatestAnalysis msg)
    {
        Sender.Tell(_state.GetLatestAlerts(msg.Location) is { } alerts
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
                ex => new RequestGrpcStreamFailed(msg.RequestId, ex));
    }
}
