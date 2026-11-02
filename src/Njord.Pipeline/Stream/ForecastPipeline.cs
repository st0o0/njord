using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Messages.Results;
using Njord.Messages.Stream;
using Terp.Akka;

namespace Njord.Pipeline.Stream;

public sealed class ForecastPipeline : ReceiveActor
{
    private const int PerProducerBufferSize = 16;

    private sealed record AnalysisGraphEnded(Exception? Cause) : IDeadLetterSuppression;

    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<IFeatureRule> _features;

    private IMaterializer? _mat;
    private Sink<DomainDataReceived, NotUsed>? _mergeSink;
    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private Sink<PipelineEvent, NotUsed>? _pipelineOutputSink;
    private Source<DomainDataReceived, NotUsed>? _inputSource;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public ForecastPipeline(TimeProvider timeProvider, IEnumerable<IFeatureRule> features)
    {
        _timeProvider = timeProvider;
        _features = [.. features];

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
        StreamRefs.SinkRef<DomainDataReceived>()
            .RecoverWithRetries<DomainDataReceived, Task<ISinkRef<DomainDataReceived>>>(_ =>
            {
                Telemetry.ProducerFailed.Add(1);
                return Source.Empty<DomainDataReceived>();
            }, 1)
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
        var features = _features;
        var timeProvider = _timeProvider;

        inputSource
            .StatefulSelectMany<DomainDataReceived, PipelineEvent, NotUsed>(() =>
            {
                var engine = new FeatureEngine(features, timeProvider);

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
            .ResumeOnError(_log)
            .WatchTermination(Keep.Right)
            .To(pipelineOutputSink)
            .Run(_mat)
            .PipeTo(Self,
                success: () => new AnalysisGraphEnded(null),
                failure: ex => new AnalysisGraphEnded(ex));
    }
}
