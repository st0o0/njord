using Akka.Actor;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class SchedulerActor : ReceivePersistentActor
{
    public override string PersistenceId => "scheduler";

    private readonly NjordOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SchedulerActor> _logger;
    private readonly Dictionary<string, ModelPollState> _states = new();
    private ISourceQueueWithComplete<WeightedTarget>? _queue;
    private readonly int _weight;

    public sealed record DataChanged(string Location, string ModelId, int Hash, DateTimeOffset Utc);

    public SchedulerActor(
        IOptions<NjordOptions> options,
        TimeProvider timeProvider,
        ILogger<SchedulerActor> logger,
        ResolvedParameterSet parameters)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _weight = WeightedTarget.ComputeWeight(parameters.HourlyCount, _options.ForecastDays);

        Recover<DataChanged>(OnRecover);
        Recover<SnapshotOffer>(_ => { });

        Command<PipelineSinkResponse>(OnSinkReceived);
        Command<PipelineSourceResponse>(OnSourceReceived);
        Command<ScheduledPoll>(OnScheduledPoll);
        Command<HashResult>(OnHashResult);
        Command<FetchFailed>(OnFetchFailed);
    }

    private static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);

    protected override void PreStart()
    {
        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSink());
        pipelineActor.Tell(new RequestPipelineSource());
    }

    private void OnRecover(DataChanged evt)
    {
        var key = Key(evt.Location, evt.ModelId);
        var state = _states.GetValueOrDefault(key, ModelPollState.Initial(_timeProvider.GetUtcNow()));
        _states[key] = state.WithDataChange(evt.Hash, evt.Utc, _options.DiscoveryInterval);
    }

    private void OnSinkReceived(PipelineSinkResponse response)
    {
        var mat = Context.Materializer();
        _queue = Source.Queue<WeightedTarget>(32, OverflowStrategy.Backpressure)
            .To(response.SinkRef.Sink)
            .Run(mat);

        _logger.LogInformation("Pipeline SinkRef received - scheduling initial polls");
        InitializeStates();
        BecomeReady();
        Stash.UnstashAll();
    }

    private void BecomeReady()
    {
        Command<PipelineSinkResponse>(_ => { });
        Command<PipelineSourceResponse>(OnSourceReceived);
        Command<ScheduledPoll>(OnScheduledPoll);
        Command<HashResult>(OnHashResult);
        Command<FetchFailed>(OnFetchFailed);
        Command<Terminated>(_ =>
        {
            _logger.LogWarning("PipelineActor terminated - waiting for new SinkRef");
            _queue?.Complete();
            _queue = null;
            var pipelineActor = Context.GetActor<PipelineActor>();
            Context.Watch(pipelineActor);
            pipelineActor.Tell(new RequestPipelineSink());
            pipelineActor.Tell(new RequestPipelineSource());
        });
    }

    private void InitializeStates()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var location in _options.Locations)
        {
            foreach (var modelId in _options.Models)
            {
                var key = Key(location.Name, modelId);
                if (!_states.ContainsKey(key))
                {
                    _states[key] = ModelPollState.Initial(now);
                }

                ScheduleNext(location.Name, modelId);
            }
        }
    }

    private void OnSourceReceived(PipelineSourceResponse response)
    {
        var mat = Context.Materializer();
        var self = Self;

        response.SourceRef.Source
            .Collect(outcome => outcome is FetchOutcome.Failure, outcome => (FetchOutcome.Failure)outcome)
            .Select(f => new FetchFailed(f.Location, f.Model.Id, f.Reason))
            .To(Sink.ActorRef<FetchFailed>(self, new Status.Success("failure-consumer-complete"), ex => new Status.Failure(ex)))
            .Run(mat);

        _logger.LogInformation("Pipeline SourceRef received - failure consumer connected");
    }

    private void OnFetchFailed(FetchFailed msg)
    {
        var key = Key(msg.Location, msg.ModelId);
        var now = _timeProvider.GetUtcNow();
        var state = _states.GetValueOrDefault(key, ModelPollState.Initial(now));

        switch (msg.Reason)
        {
            case FetchFailureReason.Transport:
                _states[key] = state.WithTransientFailure(now);
                _logger.LogWarning("Fetch failed for {Location}/{Model} (transport) - miss={Miss}",
                    msg.Location, msg.ModelId, _states[key].MissCount);
                ScheduleNext(msg.Location, msg.ModelId);
                break;

            case FetchFailureReason.RateLimited:
                var rateLimitState = state.WithTransientFailure(now);
                if (rateLimitState.NextPollUtc < now + RateLimitMinDelay)
                    rateLimitState = rateLimitState with { NextPollUtc = now + RateLimitMinDelay };
                _states[key] = rateLimitState;
                _logger.LogWarning("Fetch rate-limited for {Location}/{Model} - next poll at {Next}",
                    msg.Location, msg.ModelId, rateLimitState.NextPollUtc);
                ScheduleNext(msg.Location, msg.ModelId);
                break;

            case FetchFailureReason.ModelUnavailable:
            case FetchFailureReason.MalformedPayload:
                _logger.LogWarning("Fetch failed for {Location}/{Model} ({Reason}) - skipping until next regular poll",
                    msg.Location, msg.ModelId, msg.Reason);
                break;
        }
    }

    private void OnScheduledPoll(ScheduledPoll poll)
    {
        if (_queue is null)
        {
            return;
        }

        var location = _options.Locations.FirstOrDefault(l =>
            l.Name.Equals(poll.Location, StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            return;
        }

        var cycle = new CycleId(_timeProvider.GetUtcNow());
        var target = new WeightedTarget(location, new WeatherModel(poll.ModelId), _weight, cycle);
        _queue.OfferAsync(target);
        _logger.LogDebug("Offered poll target {Location}/{Model}", poll.Location, poll.ModelId);
    }

    private void OnHashResult(HashResult result)
    {
        var key = Key(result.Location, result.ModelId);
        var now = _timeProvider.GetUtcNow();
        var state = _states.GetValueOrDefault(key, ModelPollState.Initial(now));

        if (state.LastHash != result.Hash)
        {
            var evt = new DataChanged(result.Location, result.ModelId, result.Hash, now);
            Persist(evt, persisted =>
            {
                _states[key] = state.WithDataChange(persisted.Hash, persisted.Utc, _options.DiscoveryInterval);
                _logger.LogInformation(
                    "Data changed for {Location}/{Model} - phase={Phase}, cycle={Cycle}",
                    result.Location, result.ModelId, _states[key].Phase, _states[key].Cycle);
                ScheduleNext(result.Location, result.ModelId);
                Sender.Tell(new Ack());
            });
        }
        else
        {
            _states[key] = state.WithMiss(now, _options.DiscoveryInterval);
            _logger.LogDebug(
                "No data change for {Location}/{Model} - miss={Miss}, phase={Phase}",
                result.Location, result.ModelId, _states[key].MissCount, _states[key].Phase);
            ScheduleNext(result.Location, result.ModelId);
            Sender.Tell(new Ack());
        }
    }

    private void ScheduleNext(string location, string modelId)
    {
        var key = Key(location, modelId);
        if (!_states.TryGetValue(key, out var state))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var delay = state.NextPollUtc <= now
            ? TimeSpan.FromSeconds(1)
            : state.NextPollUtc - now;

        Context.System.Scheduler.ScheduleTellOnceCancelable(
            delay, Self, new ScheduledPoll(location, modelId), Self);
    }

    private static string Key(string location, string modelId) => $"{location}|{modelId}";
}
