using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

// Owns the long-lived hubs: producers attach to the MergeHub input via SinkRef, consumers to the
// BroadcastHub output via SourceRef. Both live as long as this actor; producers and consumers
// coming and going never rebuild them, so the analysis state in the stage closure survives them.
public sealed class ForecastPipeline : ReceiveActor
{
    private const int PerProducerBufferSize = 16;

    private sealed record AnalysisGraphEnded(Exception? Cause) : IDeadLetterSuppression;

    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<IDomainHandler> _handlers;

    private IMaterializer? _mat;
    private Sink<DomainDataReceived, NotUsed>? _mergeSink;
    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private Sink<PipelineEvent, NotUsed>? _pipelineOutputSink;
    private Source<DomainDataReceived, NotUsed>? _inputSource;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public ForecastPipeline(TimeProvider timeProvider, IEnumerable<IDomainHandler> handlers, IOptions<EnrichmentOptions> enrichments)
    {
        _timeProvider = timeProvider;
        _handlers = EnrichmentResolution.EnabledHandlers(enrichments.Value, handlers);

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
        // completion keeps the hub safe either way and counts the loss.
        StreamRefs.SinkRef<DomainDataReceived>()
            .Via(Flow.Create<DomainDataReceived>().RecoverWithRetries(_ =>
            {
                Telemetry.ProducerFailed.Add(1);
                return Source.Empty<DomainDataReceived>();
            }, 1))
            .To(_mergeSink!)
            .Run(_mat!)
            .PipeTo(Sender, Self,
                sinkRef => new RequestForecastSinkCompleted(msg.RequestId, sinkRef),
                ex => new RequestForecastSinkFailed(msg.RequestId, ex));
    }

    private void OnRequestPipelineOutput(RequestPipelineOutput msg)
    {
        _pipelineOutputSource!
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat!)
            .PipeTo(Sender, Self,
                sourceRef => new RequestPipelineOutputCompleted(msg.RequestId, sourceRef),
                ex => new RequestPipelineOutputFailed(msg.RequestId, ex));
    }

    private void OnAnalysisGraphEnded(AnalysisGraphEnded msg)
    {
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
        var inputSource = _inputSource!;
        var pipelineOutputSink = _pipelineOutputSink!;
        var handlers = _handlers;
        var timeProvider = _timeProvider;

        inputSource
            .StatefulSelectMany<DomainDataReceived, PipelineEvent, NotUsed>(() =>
            {
                var engine = new ProjectionEngine(handlers, timeProvider);

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

                    return events;
                };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .WatchTermination(Keep.Right)
            .To(pipelineOutputSink)
            .Run(_mat)
            .PipeTo(Self,
                success: () => new AnalysisGraphEnded(null),
                failure: ex => new AnalysisGraphEnded(ex));
    }
}
