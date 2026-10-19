using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Servus.Akka;

namespace Njord.Pipeline;

// Owns the long-lived hubs: producers attach to the MergeHub input via SinkRef, consumers to the
// BroadcastHub output via SourceRef. Both live as long as this incarnation; producers and consumers
// coming and going never rebuild them, so the analysis state in the stage closure survives them.
public sealed class ForecastPipeline : ReceiveActor
{
    private const int PerProducerBufferSize = 16;

    // Unique across incarnations: a restarted pipeline keeps its ActorRef, and the end of the previous
    // incarnation's graph must not fail the new one.
    private static long LastGraphId;

    private sealed record AnalysisGraphEnded(long GraphId, Exception? Cause) : IDeadLetterSuppression;

    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<IProjectionGroup> _groups;

    private IMaterializer? _mat;
    private long _graphId;
    private Sink<DomainDataReceived, NotUsed>? _mergeSink;
    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private Sink<PipelineEvent, NotUsed>? _pipelineOutputSink;
    private Source<DomainDataReceived, NotUsed>? _inputSource;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public ForecastPipeline(TimeProvider timeProvider, IEnumerable<IProjectionGroup> groups)
    {
        _timeProvider = timeProvider;
        _groups = [.. groups];

        Receive<RequestForecastSink>(OnRequestForecastSink);
        Receive<RequestPipelineOutput>(OnRequestPipelineOutput);
        Receive<AnalysisGraphEnded>(OnAnalysisGraphEnded);
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeHubs();
        MaterializeAnalysisGraph();
        _log.Info("Forecast pipeline materialized - waiting for producers and consumers");
    }

    private void OnRequestForecastSink(RequestForecastSink msg)
    {
        // A producer's failure must never end the MergeHub. Akka.NET 1.5.73 already drops a failed producer
        // (logging an error) although the MergeHub docs say it fails the hub; ending each producer's stream as a
        // completion keeps the hub safe either way and logs the loss as a warning.
        var log = _log;
        StreamRefs.SinkRef<DomainDataReceived>()
            .Via(Flow.Create<DomainDataReceived>().RecoverWithRetries(ex =>
            {
                log.Warning(ex, "Forecast producer stream failed - detaching it from the pipeline input");
                return Source.Empty<DomainDataReceived>();
            }, 1))
            .To(_mergeSink!)
            .Run(_mat!)
            .PipeTo(Sender, Self,
                sinkRef => new RequestForecastSinkCompleted(msg.RequestId, sinkRef),
                ex =>
                {
                    _log.Error(ex, "Failed to create forecast input SinkRef");
                    return new RequestForecastSinkFailed(msg.RequestId, ex);
                });
    }

    private void OnRequestPipelineOutput(RequestPipelineOutput msg)
    {
        _pipelineOutputSource!
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat!)
            .PipeTo(Sender, Self,
                sourceRef => new RequestPipelineOutputCompleted(msg.RequestId, sourceRef),
                ex =>
                {
                    _log.Error(ex, "Failed to create pipeline output SourceRef");
                    return new RequestPipelineOutputFailed(msg.RequestId, ex);
                });
    }

    private void OnAnalysisGraphEnded(AnalysisGraphEnded msg)
    {
        if (msg.GraphId != _graphId)
        {
            return;
        }

        _log.Warning(msg.Cause, "Analysis graph ended — re-materializing (hubs and consumers unaffected)");
        MaterializeAnalysisGraph();
    }

    private void MaterializeHubs()
    {
        var (mergeSink, inputSource) = MergeHub.Source<DomainDataReceived>(PerProducerBufferSize).PreMaterialize(_mat);

        var (pipelineOutputSource, pipelineOutputSink) = BroadcastHub.Sink<PipelineEvent>(bufferSize: 2)
            .PreMaterialize(_mat);

        _mergeSink = mergeSink;
        _pipelineOutputSource = pipelineOutputSource;
        _pipelineOutputSink = pipelineOutputSink;
        _inputSource = inputSource;
    }

    private void MaterializeAnalysisGraph()
    {
        var graphId = _graphId = Interlocked.Increment(ref LastGraphId);
        var inputSource = _inputSource!;
        var pipelineOutputSink = _pipelineOutputSink!;
        var groups = _groups;
        var timeProvider = _timeProvider;
        var log = _log;

        inputSource
            .StatefulSelectMany<DomainDataReceived, PipelineEvent, NotUsed>(() =>
            {
                var engine = new ProjectionEngine(groups, timeProvider);

                return received =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var events = engine.Process(received);
                    sw.Stop();
                    Telemetry.EnrichmentDuration.Record(sw.Elapsed.TotalSeconds);
                    foreach (var evt in events)
                    {
                        Telemetry.PipelineEvents.Add(1, new KeyValuePair<string, object?>("event_type", evt.GetType().Name));
                    }

                    log.Debug("Processed {0}/{1}: {2} events", received.Location.Name, received.Domain, events.Count);
                    return events;
                };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .WatchTermination(Keep.Right)
            .To(pipelineOutputSink)
            .Run(_mat)
            .PipeTo(Self,
                success: () => new AnalysisGraphEnded(graphId, null),
                failure: ex => new AnalysisGraphEnded(graphId, ex));
    }
}
