using System.Diagnostics;
using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Njord.Telemetry;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PipelineActor> _logger;

    private Sink<WeightedTarget, NotUsed>? _mergeHubSink;
    private Source<FetchOutcome, NotUsed>? _broadcastHubSource;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    private sealed record PipelineReady;

    public PipelineActor(
        IOptions<NjordOptions> options,
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        ILogger<PipelineActor> logger)
    {
        _options = options.Value;
        _client = client;
        _timeProvider = timeProvider;
        _logger = logger;

        Initializing();
    }

    protected override void PreStart()
    {
        MaterializePipeline();
    }

    private void Initializing()
    {
        Receive<PipelineReady>(_ =>
        {
            _logger.LogInformation("Pipeline graph materialized - ready to accept producers and consumers");
            Become(Ready);
            Stash.UnstashAll();
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<RequestPipelineSink>(_ =>
        {
            var sinkRef = StreamRefs.SinkRef<WeightedTarget>()
                .To(_mergeHubSink!)
                .Run(_mat!);
            sinkRef.PipeTo(Sender, Self,
                sr => new PipelineSinkResponse(sr),
                _ => null!);
        });
        Receive<RequestPipelineSource>(_ =>
        {
            var sourceRef = _broadcastHubSource!
                .RunWith(StreamRefs.SourceRef<FetchOutcome>(), _mat!);
            sourceRef.PipeTo(Sender, Self,
                sr => new PipelineSourceResponse(sr),
                _ => null!);
        });
    }

    private void MaterializePipeline()
    {
        _mat = Context.Materializer();
        var budget = _options.EffectiveBudget;
        var budgetPerMinute = (int)(budget.RequestsPerMinute * 0.8);
        var schedulerActor = Context.GetActor<SchedulerActor>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<FetchOutcome>(bufferSize: 256)
            .PreMaterialize(_mat);

        mergeHubSource
            .Throttle(budgetPerMinute, TimeSpan.FromMinutes(1), maximumBurst: 4,
                element => element.Weight, ThrottleMode.Shaping)
            .SelectAsyncUnordered(4, async target =>
            {
                using var activity = NjordTelemetry.Source.StartActivity("njord.fetch");
                activity?.SetTag("location", target.Location.Name);
                activity?.SetTag("model", target.Model.Id);
                var sw = Stopwatch.StartNew();
                var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
                sw.Stop();
                var status = outcome is FetchOutcome.Success ? "success" : "failure";
                NjordTelemetry.FetchTotal.Add(1,
                    new KeyValuePair<string, object?>("location", target.Location.Name),
                    new KeyValuePair<string, object?>("model", target.Model.Id),
                    new KeyValuePair<string, object?>("status", status));
                NjordTelemetry.FetchDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("location", target.Location.Name),
                    new KeyValuePair<string, object?>("model", target.Model.Id));
                if (outcome is FetchOutcome.Failure)
                    activity?.SetStatus(ActivityStatusCode.Error);
                return outcome;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(_ => Akka.Streams.Supervision.Directive.Resume))
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .Select(success => new HashResult(
                success.Forecast.Location,
                success.Forecast.Model.Id,
                ForecastDataHash.Compute(success.Forecast, _timeProvider)))
            .Ask<Ack>(schedulerActor, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(_ => Akka.Streams.Supervision.Directive.Resume))
            .To(Sink.Ignore<Ack>())
            .Run(_mat);

        _mergeHubSink = mergeHubSink;
        _broadcastHubSource = broadcastHubSource;

        Self.Tell(new PipelineReady());
    }
}
