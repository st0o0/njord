using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Njord.Persistence;
using Njord.Pipeline;
using Njord.Tests.Shared;
using Servus.Akka;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerActorSpec : PersistenceTestKit
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));
    private IActorRef _scheduler = null!;
    private readonly List<WeightedTarget> _offered = [];

    private IMaterializer Mat => Sys.Materializer();

    private NjordOptions Options() => new()
    {
        DiscoveryInterval = TimeSpan.FromMinutes(20),
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateScheduler(NjordOptions? options = null, string? persistenceId = null, int queueSize = 32)
    {
        var opts = options ?? Options();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var registry = ActorRegistry.For(Sys);

        var fakePipelineActor = Sys.ActorOf(
            Props.Create(() => new FakePipelineActor(_offered, Mat)));
        registry.Register<PipelineActor>(fakePipelineActor, overwrite: true);

        var props = Props.Create(() => new TestableSchedulerActor(
            opts, _time, parameters, persistenceId ?? $"scheduler-{Guid.NewGuid():N}", queueSize));

        return Sys.ActorOf(props);
    }

    private IActorRef CreateSchedulerWithSlowConsumer(NjordOptions? options = null, int queueSize = 32, int consumerDelayMs = 50)
    {
        var opts = options ?? Options();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var registry = ActorRegistry.For(Sys);

        var fakePipelineActor = Sys.ActorOf(
            Props.Create(() => new SlowFakePipelineActor(_offered, Mat, consumerDelayMs)));
        registry.Register<PipelineActor>(fakePipelineActor, overwrite: true);

        var props = Props.Create(() => new TestableSchedulerActor(
            opts, _time, parameters, $"scheduler-{Guid.NewGuid():N}", queueSize));

        return Sys.ActorOf(props);
    }

    private Task WaitForOffer(int minCount = 1) =>
        AsyncAssert.WaitUntil(() => _offered.Count >= minCount);

    [Fact(Timeout = 5000)]
    public async Task Scheduler_offers_target_after_receiving_sink_ref()
    {
        _scheduler = CreateScheduler();

        await WaitForOffer();

        Assert.True(_offered.Count > 0, "Scheduler should have offered at least one target");
        Assert.Equal("lucerne", _offered[0].Location.Name);
        Assert.Equal("icon_d2", _offered[0].Model.Id);
    }

    [Fact(Timeout = 5000)]
    public async Task Initial_polls_are_offered_without_stagger_delay()
    {
        var options = Options();
        options.Models = ["icon_d2", "gfs_seamless", "ecmwf_ifs025"];
        _scheduler = CreateScheduler(options);

        await WaitForOffer(3);

        Assert.Equal(3, _offered.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Hash_change_triggers_ack_response()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        var result = await _scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        Assert.NotNull(result);
    }

    [Fact(Timeout = 5000)]
    public async Task Unchanged_hash_also_acks()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        await _scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        var result = await _scheduler.Ask<Ack>(new HashResult("lucerne", "icon_d2", 42), TimeSpan.FromSeconds(2));
        Assert.NotNull(result);
    }

    [Fact(Timeout = 5000)]
    public async Task Transport_failure_triggers_backoff_retry()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        var countBefore = _offered.Count;
        _scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.Transport, "test"));

        await AsyncAssert.WaitUntil(() => _offered.Count > countBefore);

        Assert.True(_offered.Count > countBefore, "Transport failure should schedule a retry that offers a new target");
    }

    [Fact(Timeout = 5000)]
    public async Task Rate_limited_failure_enforces_minimum_delay()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        var countBefore = _offered.Count;
        _scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.RateLimited, "test"));

        await AsyncAssert.StaysTrue(() => _offered.Count == countBefore);
    }

    [Fact(Timeout = 5000)]
    public async Task Model_unavailable_does_not_trigger_retry()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        var countBefore = _offered.Count;
        _scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.ModelUnavailable, "test"));

        await AsyncAssert.StaysTrue(() => _offered.Count == countBefore);
    }

    [Fact(Timeout = 5000)]
    public async Task Malformed_payload_does_not_trigger_retry()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        var countBefore = _offered.Count;
        _scheduler.Tell(new FetchFailed("lucerne", "icon_d2", FetchFailureReason.MalformedPayload, "test"));

        await AsyncAssert.StaysTrue(() => _offered.Count == countBefore);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_for_all_returns_all_targets()
    {
        var options = Options();
        options.Models = ["icon_d2", "gfs_seamless"];
        _scheduler = CreateScheduler(options);
        await WaitForOffer();

        var result = await _scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("", ""), TimeSpan.FromSeconds(2));

        Assert.Equal(2, result.Count);
        Assert.Contains("lucerne/icon_d2", result.Targets);
        Assert.Contains("lucerne/gfs_seamless", result.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_for_specific_location_filters_correctly()
    {
        var options = Options();
        options.Locations =
        [
            new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
            new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
        ];
        _scheduler = CreateScheduler(options);
        await WaitForOffer(2);

        var result = await _scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("zurich", ""), TimeSpan.FromSeconds(2));

        Assert.Equal(1, result.Count);
        Assert.Contains("zurich/icon_d2", result.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_for_specific_model_filters_correctly()
    {
        var options = Options();
        options.Models = ["icon_d2", "gfs_seamless"];
        _scheduler = CreateScheduler(options);
        await WaitForOffer();

        var result = await _scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "gfs_seamless"), TimeSpan.FromSeconds(2));

        Assert.Equal(1, result.Count);
        Assert.Contains("lucerne/gfs_seamless", result.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_for_unknown_location_returns_zero()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();

        var result = await _scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("nonexistent", ""), TimeSpan.FromSeconds(2));

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Targets);
    }

    [Fact(Timeout = 5000)]
    public async Task All_initial_polls_arrive_with_small_queue_and_zero_delay()
    {
        var options = Options();
        options.Locations =
        [
            new LocationOptions { Name = "amsterdam", Latitude = 52.37, Longitude = 4.90 },
            new LocationOptions { Name = "berlin", Latitude = 52.52, Longitude = 13.41 },
        ];
        options.Models = ["icon_d2", "gfs_seamless", "ecmwf_ifs025", "icon_eu"];

        _scheduler = CreateScheduler(options, queueSize: 4);

        await WaitForOffer(8);

        Assert.Equal(8, _offered.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task All_initial_polls_arrive_with_slow_downstream()
    {
        var options = Options();
        options.Locations =
        [
            new LocationOptions { Name = "amsterdam", Latitude = 52.37, Longitude = 4.90 },
            new LocationOptions { Name = "berlin", Latitude = 52.52, Longitude = 13.41 },
        ];
        options.Models = ["icon_d2", "gfs_seamless", "ecmwf_ifs025", "icon_eu"];

        _scheduler = CreateSchedulerWithSlowConsumer(options, queueSize: 4, consumerDelayMs: 50);

        await WaitForOffer(8);

        Assert.Equal(8, _offered.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Trigger_immediate_poll_actually_offers_target_to_pipeline()
    {
        _scheduler = CreateScheduler();
        await WaitForOffer();
        var countBefore = _offered.Count;

        await _scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll("lucerne", "icon_d2"), TimeSpan.FromSeconds(2));

        await AsyncAssert.WaitUntil(() => _offered.Count > countBefore);
        var latest = _offered[^1];
        Assert.Equal("lucerne", latest.Location.Name);
        Assert.Equal("icon_d2", latest.Model.Id);
    }

    private sealed class SlowFakePipelineActor : ReceiveActor
    {
        public SlowFakePipelineActor(List<WeightedTarget> offered, IMaterializer mat, int delayMs)
        {
            Receive<RequestPipelineSink>(_ =>
            {
                var (hubSink, hubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 8)
                    .PreMaterialize(mat);

                hubSource
                    .SelectAsync(1, async t =>
                    {
                        await Task.Delay(delayMs);
                        return t;
                    })
                    .RunWith(Sink.ForEach<WeightedTarget>(t => offered.Add(t)), mat);

                var sinkRef = StreamRefs.SinkRef<WeightedTarget>()
                    .To(hubSink)
                    .Run(mat)
                    .Result;

                Sender.Tell(new PipelineSinkResponse(sinkRef));
            });

            Receive<RequestPipelineSource>(_ =>
            {
                var sourceRef = Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;

                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }
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

            Receive<RequestPipelineSource>(_ =>
            {
                var sourceRef = Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;

                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }
    }

    private sealed class TestableSchedulerActor : ReceivePersistentActor
    {
        public override string PersistenceId { get; }

        private static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);

        private readonly NjordOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly int _queueSize;
        private readonly Dictionary<string, ModelPollState> _states = new();
        private ISourceQueueWithComplete<WeightedTarget>? _queue;
        private readonly int _weight;
        private bool _sourceReceived;

        public TestableSchedulerActor(
            NjordOptions options,
            TimeProvider timeProvider,
            ResolvedParameterSet parameters,
            string persistenceId,
            int queueSize = 32)
        {
            PersistenceId = persistenceId;
            _options = options;
            _timeProvider = timeProvider;
            _queueSize = queueSize;
            _weight = WeightedTarget.ComputeWeight(parameters.HourlyCount, options.ForecastDays);

            Recover<DataChangedDto>(dto => OnRecover(SchedulerDtoMapping.ToDomain(dto)));
            Recover<SnapshotOffer>(_ => { });

            WaitingForRefs();
        }

        protected override void PreStart()
        {
            var pipelineActor = Context.GetActor<PipelineActor>();
            pipelineActor.Tell(new RequestPipelineSink());
            pipelineActor.Tell(new RequestPipelineSource());
        }

        private void OnRecover(SchedulerActor.DataChanged evt)
        {
            var key = $"{evt.Location}|{evt.ModelId}";
            var state = _states.GetValueOrDefault(key, ModelPollState.Initial(_timeProvider.GetUtcNow()));
            _states[key] = state.WithDataChange(evt.Hash, evt.Utc, _options.DiscoveryInterval);
        }

        private void WaitingForRefs()
        {
            Command<PipelineSinkResponse>(response =>
            {
                var mat = Context.Materializer();
                _queue = Source.Queue<WeightedTarget>(_queueSize, OverflowStrategy.Backpressure)
                    .To(response.SinkRef.Sink)
                    .Run(mat);
                TryTransitionToConnecting();
            });
            Command<PipelineSourceResponse>(_ =>
            {
                _sourceReceived = true;
                TryTransitionToConnecting();
            });
            CommandAny(_ => Stash.Stash());
        }

        private sealed record ConnectionEstablished;
        private sealed record OfferFailed(string Location, string ModelId, Exception Error);

        private void TryTransitionToConnecting()
        {
            if (_queue is null || !_sourceReceived)
                return;

            var now = _timeProvider.GetUtcNow();
            foreach (var location in _options.Locations)
                foreach (var modelId in _options.Models)
                {
                    var key = $"{location.Name}|{modelId}";
                    if (!_states.ContainsKey(key))
                    {
                        _states[key] = ModelPollState.Initial(now);
                    }

                    ScheduleNext(location.Name, modelId);
                }

            Become(Connecting);
        }

        private void Connecting()
        {
            Command<ScheduledPoll>(poll =>
            {
                var target = CreateTarget(poll);
                if (target is null)
                    return;

                _queue!.OfferAsync(target).PipeTo(Self,
                    success: _ => new ConnectionEstablished(),
                    failure: ex => new OfferFailed(poll.Location, poll.ModelId, ex));
                Become(WaitingForConnection);
            });
            CommandAny(_ => Stash.Stash());
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
                Self.Tell(new ScheduledPoll(msg.Location, msg.ModelId));
                Become(Connecting);
            });
            CommandAny(_ => Stash.Stash());
        }

        private void Ready()
        {
            Command<PipelineSinkResponse>(_ => { });
            Command<PipelineSourceResponse>(_ => { });
            CommandAsync<ScheduledPoll>(OnScheduledPoll);
            Command<HashResult>(OnHashResult);
            Command<FetchFailed>(OnFetchFailed);
            Command<TriggerImmediatePoll>(OnTriggerImmediatePoll);
        }

        private async Task OnScheduledPoll(ScheduledPoll poll)
        {
            var target = CreateTarget(poll);
            if (target is null)
                return;

            await _queue!.OfferAsync(target);
        }

        private WeightedTarget? CreateTarget(ScheduledPoll poll)
        {
            if (_queue is null)
                return null;

            var location = _options.Locations.FirstOrDefault(l =>
                l.Name.Equals(poll.Location, StringComparison.OrdinalIgnoreCase));
            if (location is null)
                return null;

            var cycle = new CycleId(_timeProvider.GetUtcNow());
            return new WeightedTarget(location, new WeatherModel(poll.ModelId), _weight, cycle);
        }

        private void OnHashResult(HashResult result)
        {
            var key = $"{result.Location}|{result.ModelId}";
            var now = _timeProvider.GetUtcNow();
            var state = _states.GetValueOrDefault(key, ModelPollState.Initial(now));

            if (state.LastHash != result.Hash)
            {
                var evt = new SchedulerActor.DataChanged(result.Location, result.ModelId, result.Hash, now);
                Persist(SchedulerDtoMapping.ToDto(evt), _ =>
                {
                    _states[key] = state.WithDataChange(evt.Hash, evt.Utc, _options.DiscoveryInterval);
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

        private void OnFetchFailed(FetchFailed msg)
        {
            var key = $"{msg.Location}|{msg.ModelId}";
            var now = _timeProvider.GetUtcNow();
            var state = _states.GetValueOrDefault(key, ModelPollState.Initial(now));

            switch (msg.Reason)
            {
                case FetchFailureReason.Transport:
                    _states[key] = state.WithTransientFailure(now);
                    ScheduleNext(msg.Location, msg.ModelId);
                    break;

                case FetchFailureReason.RateLimited:
                    var rateLimitState = state.WithTransientFailure(now);
                    if (rateLimitState.NextPollUtc < now + RateLimitMinDelay)
                    {
                        rateLimitState = rateLimitState with { NextPollUtc = now + RateLimitMinDelay };
                    }

                    _states[key] = rateLimitState;
                    ScheduleNext(msg.Location, msg.ModelId);
                    break;

                case FetchFailureReason.ModelUnavailable:
                case FetchFailureReason.MalformedPayload:
                    break;
            }
        }

        private void OnTriggerImmediatePoll(TriggerImmediatePoll msg)
        {
            var targets = new List<string>();
            var locations = string.IsNullOrEmpty(msg.Location)
                ? _options.Locations
                : _options.Locations.Where(l => l.Name.Equals(msg.Location, StringComparison.OrdinalIgnoreCase));

            foreach (var location in locations)
            {
                var models = string.IsNullOrEmpty(msg.Model)
                    ? location.ResolveModels(_options.Models)
                    : location.ResolveModels(_options.Models)
                        .Where(m => m.Equals(msg.Model, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                foreach (var modelId in models)
                {
                    Self.Tell(new ScheduledPoll(location.Name, modelId));
                    targets.Add($"{location.Name}/{modelId}");
                }
            }

            Sender.Tell(new TriggerPollResult(targets.Count, targets));
        }

        private void ScheduleNext(string location, string modelId)
        {
            var key = $"{location}|{modelId}";
            if (!_states.TryGetValue(key, out var state))
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var msg = new ScheduledPoll(location, modelId);

            if (state.NextPollUtc <= now)
            {
                Self.Tell(msg);
            }
            else
            {
                var delay = state.NextPollUtc - now;
                if (delay > TimeSpan.FromMilliseconds(500))
                    delay = TimeSpan.FromMilliseconds(500);

                Context.System.Scheduler.ScheduleTellOnceCancelable(
                    delay, Self, msg, Self);
            }
        }
    }
}
