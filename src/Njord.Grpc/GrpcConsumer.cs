using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Messages.Fetch;
using Njord.Messages.Stream;
using Terp.Akka;

namespace Njord.Grpc;

public sealed class GrpcConsumer : ReceiveActor
{
    private const int StreamClientBufferSize = 256;
    private static readonly TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _clientIdleTimeout = TimeSpan.FromSeconds(90);

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IMaterializer _mat = null!;

    private IActorRef _hubInput = ActorRefs.Nobody;
    private Source<PipelineEvent, NotUsed> _clientHubSource = Source.Empty<PipelineEvent>();

    private GrpcConsumerState _state = GrpcConsumerState.Empty;

    private sealed record StreamCompleted;

    public GrpcConsumer()
    {
        Receive<QueryDomainData>(HandleQueryDomainData);
        Receive<QueryLatestAnalysis>(HandleQueryLatestAnalysis);
        Receive<RequestGrpcStream>(HandleStreamRequest);
        Receive<PipelineEvent>(OnPipelineEvent);
        Receive<StreamCompleted>(_ => ReconnectToPipeline());
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

        var pipeline = Context.GetActor<IForecastPipeline>();
        pipeline.Tell(new RequestPipelineOutput(0));
    }

    private void ReconnectToPipeline()
    {
        _log.Info("Pipeline SourceRef stream completed — reconnecting");
        var pipeline = Context.GetActor<IForecastPipeline>();
        pipeline.Tell(new RequestPipelineOutput(0));
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
        Sender.Tell(_state.GetDomainData(msg.Location, msg.Domain, msg.ModelId) is { } latest
            ? new DomainDataResult(latest)
            : new DomainDataNotAvailable(msg.Location, msg.Domain, msg.ModelId));
    }

    private void HandleQueryLatestAnalysis(QueryLatestAnalysis msg)
    {
        var results = _state.GetLatestResults(msg.Location);
        Sender.Tell(results.Count == 0
            ? new LatestAnalysisNotAvailable(msg.Location)
            : new LatestAnalysisResult(msg.Location, results));
    }

    private void HandleStreamRequest(RequestGrpcStream msg)
    {
        var snapshot = _state.DomainData.Values
            .Concat<PipelineEvent>(_state.Results.Values);

        Source.From(snapshot)
            .Concat(_clientHubSource)
            .KeepAlive<PipelineEvent, PipelineEvent, NotUsed>(_keepAliveInterval, () => Heartbeat.Instance)
            .Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)
            .IdleTimeout(_clientIdleTimeout)
            .Where(e => e is not Heartbeat)
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat)
            .PipeTo(Sender, Self,
                sr => new RequestGrpcStreamCompleted(msg.RequestId, sr),
                ex => new RequestGrpcStreamFailed(msg.RequestId, ex));
    }
}
