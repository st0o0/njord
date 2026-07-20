using System.Diagnostics;
using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Ingest;
using Njord.Pipeline;
using Njord.Tests.Shared;
using Microsoft.Extensions.Logging;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

public sealed class PipelineConnectionSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
    }

    [Fact(Timeout = 10000)]
    public async Task Persistent_scheduler_connects_and_offers_through_full_pipeline()
    {
        var offered = new TaskCompletionSource<int>();

        var pipeline = Sys.ActorOf(
            Props.Create(() => new ProductionLikePipelineActor(offered)),
            "pipeline");
        ActorRegistry.Register<PipelineActor>(pipeline, overwrite: true);

        var scheduler = Sys.ActorOf(
            Props.Create(() => new ProductionLikeSchedulerActor($"sched-{Guid.NewGuid():N}")),
            "scheduler");

        var value = await offered.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(42, value);
    }

    [Fact(Timeout = 10000)]
    public async Task Real_scheduler_and_pipeline_actors_connect_and_poll()
    {
        var fetchCalled = new TaskCompletionSource<string>();

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "test", Latitude = 47.0, Longitude = 8.3 }],
            Models = ["icon_d2"],
            DiscoveryInterval = TimeSpan.FromMinutes(20),
        };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var optionsMonitor = new FakeOptionsMonitor(options);
        IBudgetGate<WeightedTarget> gate = new WeightedBudgetGate(
            new OptionsBudgetProvider(optionsMonitor), new BudgetTracker());
        var client = new FakeOpenMeteoClient(fetchCalled);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));
        var health = new NjordHealthState { ServiceStartedUtc = time.GetUtcNow() };

        // Register SchedulerActor placeholder FIRST (PipelineActor.MaterializePipeline
        // needs GetActor<SchedulerActor> for the inline feedback consumer)
        var schedulerPlaceholder = Sys.ActorOf(Props.Create(() => new BlackholeActor()), "sched-placeholder");
        ActorRegistry.Register<SchedulerActor>(schedulerPlaceholder, overwrite: true);

        var pipeline = Sys.ActorOf(
            Props.Create(() => new PipelineActor(client, time, gate, NullLogger<PipelineActor>())),
            "real-pipeline");
        ActorRegistry.Register<PipelineActor>(pipeline, overwrite: true);

        // Simulate other actors requesting SourceRefs (like ModelStateActor, EnrichmentActor)
        for (var i = 0; i < 3; i++)
        {
            var sourceRef = await pipeline.Ask<PipelineSourceResponse>(
                new Njord.Pipeline.RequestPipelineSource(), TimeSpan.FromSeconds(5));
            sourceRef.SourceRef.Source.RunWith(Sink.Ignore<FetchOutcome>(), Sys.Materializer());
        }

        var scheduler = Sys.ActorOf(
            Props.Create(() => new SchedulerActor(
                Options.Create(options), time, NullLogger<SchedulerActor>(), parameters, health)),
            "real-scheduler");
        ActorRegistry.Register<SchedulerActor>(scheduler, overwrite: true);

        var location = await fetchCalled.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal("test", location);
    }

    [Fact(Timeout = 10000)]
    public async Task Requests_have_no_serialization_gap_from_scheduler()
    {
        var fetchTimestamps = new System.Collections.Concurrent.ConcurrentBag<long>();
        var allFetched = new TaskCompletionSource();
        var expectedCount = 8;

        var options = new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "loc-a", Latitude = 47.0, Longitude = 8.3 },
                new LocationOptions { Name = "loc-b", Latitude = 52.5, Longitude = 13.4 },
            ],
            Models = ["model_1", "model_2", "model_3", "model_4"],
            DiscoveryInterval = TimeSpan.FromMinutes(20),
        };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        IBudgetGate<WeightedTarget> gate = new AlwaysAllowWeightedGate();
        var client = new TimingOpenMeteoClient(fetchTimestamps, expectedCount, allFetched);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));
        var health = new NjordHealthState { ServiceStartedUtc = time.GetUtcNow() };

        var schedulerPlaceholder = Sys.ActorOf(Props.Create(() => new BlackholeActor()), "sched-ph");
        ActorRegistry.Register<SchedulerActor>(schedulerPlaceholder, overwrite: true);

        var pipeline = Sys.ActorOf(
            Props.Create(() => new PipelineActor(client, time, gate, NullLogger<PipelineActor>())),
            "timing-pipeline");
        ActorRegistry.Register<PipelineActor>(pipeline, overwrite: true);

        var scheduler = Sys.ActorOf(
            Props.Create(() => new SchedulerActor(
                Options.Create(options), time, NullLogger<SchedulerActor>(), parameters, health)),
            "timing-scheduler");
        ActorRegistry.Register<SchedulerActor>(scheduler, overwrite: true);

        await allFetched.Task.WaitAsync(TimeSpan.FromSeconds(8));

        var timestamps = fetchTimestamps.OrderBy(t => t).ToList();
        Assert.Equal(expectedCount, timestamps.Count);

        var gaps = new List<double>();
        for (var i = 1; i < timestamps.Count; i++)
        {
            var gapMs = (timestamps[i] - timestamps[i - 1]) / (double)TimeSpan.TicksPerMillisecond;
            gaps.Add(gapMs);
        }

        var maxGap = gaps.Max();
        var avgGap = gaps.Average();

        // With AlwaysAllow gate: no throttle, all requests should be near-instant
        // With real BudgetGate: ~125ms per token at 8 tokens/sec is expected
        // The key assertion: no >1s gaps from actor serialization (the original bug was ~1.5s)
        Assert.True(maxGap < 1000, $"Max gap between requests was {maxGap:F0}ms — expected <1000ms (no actor serialization). Gaps: [{string.Join(", ", gaps.Select(g => $"{g:F0}ms"))}]");
    }

    private sealed class TimingOpenMeteoClient : IOpenMeteoClient
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<long> _timestamps;
        private readonly int _expectedCount;
        private readonly TaskCompletionSource _allFetched;
        private int _count;

        public TimingOpenMeteoClient(
            System.Collections.Concurrent.ConcurrentBag<long> timestamps,
            int expectedCount,
            TaskCompletionSource allFetched)
        {
            _timestamps = timestamps;
            _expectedCount = expectedCount;
            _allFetched = allFetched;
        }

        public Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken ct)
        {
            _timestamps.Add(Stopwatch.GetTimestamp());
            if (Interlocked.Increment(ref _count) >= _expectedCount)
                _allFetched.TrySetResult();

            var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
            var forecast = new ModelForecast(model, location.Name, cycle,
                new ForecastSeries([new ForecastPoint(DateTimeOffset.UtcNow, new Dictionary<ParameterDef, double?> { [temp] = 20.0 })]),
                DailyForecastSeries.Empty);
            return Task.FromResult<FetchOutcome>(new FetchOutcome.Success(forecast));
        }
    }

    private static ILogger<T> NullLogger<T>() =>
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<T>();

    private sealed class FakeOptionsMonitor(NjordOptions value) : IOptionsMonitor<NjordOptions>
    {
        public NjordOptions CurrentValue => value;
        public NjordOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<NjordOptions, string?> listener) => null;
    }

    private sealed class FakeOpenMeteoClient : IOpenMeteoClient
    {
        private readonly TaskCompletionSource<string> _fetchCalled;

        public FakeOpenMeteoClient(TaskCompletionSource<string> fetchCalled)
        {
            _fetchCalled = fetchCalled;
        }

        public Task<FetchOutcome> FetchAsync(LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken ct)
        {
            _fetchCalled.TrySetResult(location.Name);
            var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
            var forecast = new ModelForecast(model, location.Name, cycle,
                new ForecastSeries([new ForecastPoint(DateTimeOffset.UtcNow, new Dictionary<ParameterDef, double?> { [temp] = 20.0 })]),
                DailyForecastSeries.Empty);
            return Task.FromResult<FetchOutcome>(new FetchOutcome.Success(forecast));
        }
    }

    private sealed class ProductionLikePipelineActor : ReceiveActor, IWithStash
    {
        public IStash Stash { get; set; } = null!;

        private sealed record PipelineReady;

        public ProductionLikePipelineActor(TaskCompletionSource<int> offered)
        {
            var mat = Context.Materializer();
            var blackhole = Context.ActorOf(Props.Create(() => new BlackholeActor()));

            var (mergeHubSink, mergeHubSource) = MergeHub.Source<int>(perProducerBufferSize: 16)
                .PreMaterialize(mat);

            var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<int>(bufferSize: 2)
                .PreMaterialize(mat);

            // Full pipeline: MergeHub → Throttle → SelectAsync → Buffer → BroadcastHub
            mergeHubSource
                .Via(new BudgetThrottleStage<int>(new AlwaysAllowGate()))
                .SelectAsyncUnordered(2, x => Task.FromResult(x))
                .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                    _ => Akka.Streams.Supervision.Directive.Resume))
                .Buffer(32, OverflowStrategy.Backpressure)
                .To(broadcastHubSink)
                .Run(mat);

            // Inline feedback consumer with Ask (like production)
            broadcastHubSource
                .Ask<string>(blackhole, TimeSpan.FromSeconds(5))
                .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                    _ => Akka.Streams.Supervision.Directive.Resume))
                .To(Sink.Ignore<string>())
                .Run(mat);

            // Egress consumer (like ModelStateActor/EnrichmentActor)
            broadcastHubSource
                .RunWith(Sink.ForEach<int>(x => offered.TrySetResult(x)), mat);

            Receive<RequestPipelineSink>(_ =>
            {
                StreamRefs.SinkRef<int>()
                    .To(mergeHubSink)
                    .Run(mat)
                    .PipeTo(Sender, Self,
                        sr => new SinkRefResponse(sr),
                        _ => null!);
            });
            Receive<RequestPipelineSource>(_ =>
            {
                broadcastHubSource
                    .RunWith(StreamRefs.SourceRef<int>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new SourceRefResponse(sr),
                        _ => null!);
            });
        }
    }

    private sealed class ProductionLikeSchedulerActor : ReceivePersistentActor
    {
        public override string PersistenceId { get; }

        private IMaterializer _mat = null!;
        private ISourceQueueWithComplete<int>? _queue;
        private bool _sourceReceived;

        private sealed record ConnectionEstablished;
        private sealed record OfferFailed(Exception Error);
        private sealed record Evt(int Value);

        public ProductionLikeSchedulerActor(string persistenceId)
        {
            PersistenceId = persistenceId;

            Recover<Evt>(_ => { });
            Recover<SnapshotOffer>(_ => { });

            WaitingForRefs();
        }

        protected override void PreStart()
        {
            _mat = Context.Materializer();
            var pipeline = Context.GetActor<PipelineActor>();
            pipeline.Tell(new RequestPipelineSink());
            pipeline.Tell(new RequestPipelineSource());
        }

        private void WaitingForRefs()
        {
            Command<SinkRefResponse>(response =>
            {
                _queue = Source.Queue<int>(4, OverflowStrategy.Backpressure)
                    .To(response.SinkRef.Sink)
                    .Run(_mat);
                TryConnect();
            });
            Command<SourceRefResponse>(response =>
            {
                // Materialize failure consumer with Sink.ActorRef (like production)
                response.SourceRef.Source
                    .Collect(x => x < 0, x => x)
                    .To(Sink.ActorRef<int>(Self, new Status.Success("done"),
                        ex => new Status.Failure(ex)))
                    .Run(_mat);

                _sourceReceived = true;
                TryConnect();
            });
            Command<SchedulePoll>(_ => Stash.Stash());
            Command<ConnectionEstablished>(_ => Stash.Stash());
        }

        private void TryConnect()
        {
            if (_queue is null || !_sourceReceived)
                return;

            Self.Tell(new SchedulePoll());
            Become(Connecting);
        }

        private sealed record SchedulePoll;

        private void Connecting()
        {
            Command<SchedulePoll>(_ =>
            {
                _queue!.OfferAsync(42).PipeTo(Self,
                    success: _ => new ConnectionEstablished(),
                    failure: ex => new OfferFailed(ex));
                Become(WaitingForConnection);
            });
            Command<ConnectionEstablished>(_ => Stash.Stash());
        }

        private void WaitingForConnection()
        {
            Command<ConnectionEstablished>(_ =>
            {
                Become(Ready);
                Stash.UnstashAll();
            });
            Command<OfferFailed>(msg =>
            {
                Self.Tell(new SchedulePoll());
                Become(Connecting);
            });
            Command<SchedulePoll>(_ => Stash.Stash());
        }

        private void Ready()
        {
            Command<SchedulePoll>(_ => { });
        }
    }

    private sealed record RequestPipelineSink;
    private sealed record SinkRefResponse(ISinkRef<int> SinkRef);
    private sealed record RequestPipelineSource;
    private sealed record SourceRefResponse(ISourceRef<int> SourceRef);

    private sealed class AlwaysAllowWeightedGate : IBudgetGate<WeightedTarget>
    {
        public bool TryAcquire(WeightedTarget element) => true;
        public TimeSpan EstimateDelay(WeightedTarget element) => TimeSpan.Zero;
    }

    private sealed class AlwaysAllowGate : IBudgetGate<int>
    {
        public bool TryAcquire(int element) => true;
        public TimeSpan EstimateDelay(int element) => TimeSpan.Zero;
    }

    private sealed class BlackholeActor : ReceiveActor
    {
        public BlackholeActor()
        {
            ReceiveAny(_ => { });
        }
    }
}
