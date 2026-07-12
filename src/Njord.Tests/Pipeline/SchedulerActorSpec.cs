using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerActorSpec : IAsyncLifetime
{
    private static readonly Config InMemoryPersistence = ConfigurationFactory.ParseString("""
        akka.persistence {
            journal.plugin = "akka.persistence.journal.inmem"
            snapshot-store.plugin = "akka.persistence.snapshot-store.inmem"
        }
        """);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private ActorSystem _system = null!;
    private IMaterializer _mat = null!;
    private IActorRef _scheduler = null!;
    private readonly List<WeightedTarget> _offered = [];

    private NjordOptions Options() => new()
    {
        DiscoveryInterval = TimeSpan.FromMinutes(20),
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    public ValueTask InitializeAsync()
    {
        _system = ActorSystem.Create("scheduler-spec-" + Guid.NewGuid().ToString("N")[..8], InMemoryPersistence);
        _mat = _system.Materializer();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _system.Terminate();
    }

    private IActorRef CreateScheduler(NjordOptions? options = null, string? persistenceId = null)
    {
        var opts = options ?? Options();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var registry = new ActorRegistry();

        var fakePipelineActor = _system.ActorOf(
            Props.Create(() => new FakePipelineActor(_offered, _mat)));
        registry.Register<PipelineActor>(fakePipelineActor);

        var props = Props.Create(() => new TestableSchedulerActor(
            opts, _time, parameters, registry, persistenceId ?? $"scheduler-{Guid.NewGuid():N}"));

        return _system.ActorOf(props);
    }

    [Fact(Timeout = 5000)]
    public async Task Scheduler_offers_target_after_receiving_sink_ref()
    {
        _scheduler = CreateScheduler();

        await Task.Delay(500);

        Assert.True(_offered.Count > 0, "Scheduler should have offered at least one target");
        Assert.Equal("lucerne", _offered[0].Location.Name);
        Assert.Equal("icon_d2", _offered[0].Model.Id);
    }

    [Fact(Timeout = 5000)]
    public async Task Hash_change_triggers_ack_response()
    {
        _scheduler = CreateScheduler();
        await Task.Delay(300);

        var result = await _scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        Assert.NotNull(result);
    }

    [Fact(Timeout = 5000)]
    public async Task Unchanged_hash_also_acks()
    {
        _scheduler = CreateScheduler();
        await Task.Delay(300);

        await _scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        var result = await _scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        Assert.NotNull(result);
    }

    [Fact(Timeout = 5000)]
    public async Task RefreshModel_offers_target_immediately()
    {
        _scheduler = CreateScheduler();
        await Task.Delay(300);

        var countBefore = _offered.Count;
        _scheduler.Tell(new PipelineCommand.RefreshModel("lucerne", new WeatherModel("icon_d2")));
        await Task.Delay(200);

        Assert.True(_offered.Count > countBefore);
    }

    [Fact(Timeout = 5000)]
    public async Task RefreshLocation_offers_all_models()
    {
        var options = Options();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        _scheduler = CreateScheduler(options);
        await Task.Delay(300);

        var countBefore = _offered.Count;
        _scheduler.Tell(new PipelineCommand.RefreshLocation("lucerne"));
        await Task.Delay(200);

        Assert.True(_offered.Count >= countBefore + 2);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_location_refresh_is_ignored()
    {
        _scheduler = CreateScheduler();
        await Task.Delay(300);

        var countBefore = _offered.Count;
        _scheduler.Tell(new PipelineCommand.RefreshLocation("atlantis"));
        await Task.Delay(200);

        Assert.Equal(countBefore, _offered.Count);
    }

    private sealed class FakePipelineActor : ReceiveActor
    {
        public FakePipelineActor(List<WeightedTarget> offered, IMaterializer mat)
        {
            Receive<RequestPipelineSink>(_ =>
            {
                var (hubSink, hubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 8)
                    .PreMaterialize(mat);

                hubSource
                    .RunWith(Sink.ForEach<WeightedTarget>(t => offered.Add(t)), mat);

                var sinkRef = StreamRefs.SinkRef<WeightedTarget>()
                    .To(hubSink)
                    .Run(mat)
                    .Result;

                Sender.Tell(new PipelineSinkResponse(sinkRef));
            });
        }
    }

    private sealed class TestableSchedulerActor : ReceivePersistentActor
    {
        public override string PersistenceId { get; }

        private readonly NjordOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ActorRegistry _registry;
        private readonly Dictionary<string, ModelPollState> _states = new();
        private ISourceQueueWithComplete<WeightedTarget>? _queue;
        private readonly int _weight;

        public TestableSchedulerActor(
            NjordOptions options,
            TimeProvider timeProvider,
            ResolvedParameterSet parameters,
            ActorRegistry registry,
            string persistenceId)
        {
            PersistenceId = persistenceId;
            _options = options;
            _timeProvider = timeProvider;
            _registry = registry;
            _weight = WeightedTarget.ComputeWeight(parameters.HourlyCount, options.ForecastDays);

            Recover<SchedulerActor.DataChanged>(OnRecover);
            Recover<SnapshotOffer>(_ => { });

            Command<PipelineSinkResponse>(OnSinkReceived);
            Command<ScheduledPoll>(OnScheduledPoll);
            Command<HashResult>(OnHashResult);
            Command<PipelineCommand.RefreshModel>(OnRefreshModel);
            Command<PipelineCommand.RefreshLocation>(OnRefreshLocation);
        }

        protected override void PreStart()
        {
            var pipelineActor = _registry.Get<PipelineActor>();
            pipelineActor.Tell(new RequestPipelineSink());
        }

        private void OnRecover(SchedulerActor.DataChanged evt)
        {
            var key = $"{evt.Location}|{evt.ModelId}";
            var state = _states.GetValueOrDefault(key, ModelPollState.Initial(_timeProvider.GetUtcNow()));
            _states[key] = state.WithDataChange(evt.Hash, evt.Utc, _options.DiscoveryInterval);
        }

        private void OnSinkReceived(PipelineSinkResponse response)
        {
            var mat = Context.Materializer();
            _queue = Source.Queue<WeightedTarget>(32, OverflowStrategy.Backpressure)
                .To(response.SinkRef.Sink)
                .Run(mat);

            var now = _timeProvider.GetUtcNow();
            foreach (var location in _options.Locations)
                foreach (var modelId in _options.Models)
                {
                    var key = $"{location.Name}|{modelId}";
                    if (!_states.ContainsKey(key))
                        _states[key] = ModelPollState.Initial(now);
                    ScheduleNext(location.Name, modelId);
                }
            Stash.UnstashAll();
        }

        private void OnScheduledPoll(ScheduledPoll poll)
        {
            if (_queue is null) return;
            var location = _options.Locations.FirstOrDefault(l =>
                l.Name.Equals(poll.Location, StringComparison.OrdinalIgnoreCase));
            if (location is null) return;
            _queue.OfferAsync(new WeightedTarget(location, new WeatherModel(poll.ModelId), _weight));
        }

        private void OnHashResult(HashResult result)
        {
            var key = $"{result.Location}|{result.ModelId}";
            var now = _timeProvider.GetUtcNow();
            var state = _states.GetValueOrDefault(key, ModelPollState.Initial(now));

            if (state.LastHash != result.Hash)
            {
                var evt = new SchedulerActor.DataChanged(result.Location, result.ModelId, result.Hash, now);
                Persist(evt, persisted =>
                {
                    _states[key] = state.WithDataChange(persisted.Hash, persisted.Utc, _options.DiscoveryInterval);
                    ScheduleNext(result.Location, result.ModelId);
                    Sender.Tell(new Ack());
                });
            }
            else
            {
                _states[key] = state.WithMiss(now, _options.DiscoveryInterval);
                ScheduleNext(result.Location, result.ModelId);
                Sender.Tell(new Ack());
            }
        }

        private void OnRefreshModel(PipelineCommand.RefreshModel cmd)
        {
            if (_queue is null) return;
            var location = _options.Locations.FirstOrDefault(l =>
                l.Name.Equals(cmd.Location, StringComparison.OrdinalIgnoreCase));
            if (location is null) return;
            if (!_options.Models.Contains(cmd.Model.Id, StringComparer.OrdinalIgnoreCase)) return;
            _queue.OfferAsync(new WeightedTarget(location, cmd.Model, _weight));
        }

        private void OnRefreshLocation(PipelineCommand.RefreshLocation cmd)
        {
            if (_queue is null) return;
            var location = _options.Locations.FirstOrDefault(l =>
                l.Name.Equals(cmd.Location, StringComparison.OrdinalIgnoreCase));
            if (location is null) return;
            foreach (var modelId in _options.Models)
                _queue.OfferAsync(new WeightedTarget(location, new WeatherModel(modelId), _weight));
        }

        private void ScheduleNext(string location, string modelId)
        {
            var key = $"{location}|{modelId}";
            if (!_states.TryGetValue(key, out var state)) return;
            var now = _timeProvider.GetUtcNow();
            var delay = state.NextPollUtc <= now ? TimeSpan.FromMilliseconds(100) : state.NextPollUtc - now;
            if (delay > TimeSpan.FromSeconds(30)) delay = TimeSpan.FromSeconds(30);
            Context.System.Scheduler.ScheduleTellOnce(delay, Self, new ScheduledPoll(location, modelId), Self);
        }
    }
}
