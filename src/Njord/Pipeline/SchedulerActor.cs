using Akka.Actor;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Ingest;
using Njord.Persistence;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class SchedulerActor : ReceivePersistentActor
{
    public override string PersistenceId => "scheduler";

    private IMaterializer _mat = null!;
    private readonly NjordOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SchedulerActor> _logger;
    private readonly NjordHealthState _healthState;
    private readonly Dictionary<string, ModelPollState> _states = new();
    private ISourceQueueWithComplete<WeightedTarget>? _queue;
    private readonly int _weight;
    private bool _sourceReceived;

    public sealed record DataChanged(string Location, string ModelId, int Hash, DateTimeOffset Utc);

    private sealed record ConnectionEstablished;
    private sealed record OfferFailed(string Location, string ModelId, Exception Error);

    private static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);

    public SchedulerActor(
        IOptions<NjordOptions> options,
        TimeProvider timeProvider,
        ILogger<SchedulerActor> logger,
        ResolvedParameterSet parameters,
        NjordHealthState healthState)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _healthState = healthState;
        _weight = WeightedTarget.ComputeWeight(parameters.HourlyCount, _options.ForecastDays);

        Recover<DataChangedDto>(dto => OnRecover(SchedulerDtoMapping.ToDomain(dto)));
        Recover<SnapshotOffer>(_ => { });

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
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

    private void StashKnownCommands()
    {
        Command<ScheduledPoll>(_ => Stash.Stash());
        Command<HashResult>(_ => Stash.Stash());
        Command<FetchFailed>(_ => Stash.Stash());
        Command<TriggerImmediatePoll>(_ => Stash.Stash());
    }

    private void WaitingForRefs()
    {
        Command<PipelineSinkResponse>(response =>
        {
            _queue = Source.Queue<WeightedTarget>(16, OverflowStrategy.Backpressure)
                .To(response.SinkRef.Sink)
                .Run(_mat);
            _logger.LogInformation("Pipeline SinkRef received");
            TryTransitionToConnecting();
        });
        Command<PipelineSourceResponse>(response =>
        {
            OnSourceReceived(response);
            TryTransitionToConnecting();
        });
        Command<Terminated>(OnTerminated);
        StashKnownCommands();
    }

    private void TryTransitionToConnecting()
    {
        if (_queue is null || !_sourceReceived)
            return;

        _logger.LogInformation("Pipeline refs received - connecting");
        InitializeStates();
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
        Command<Terminated>(OnTerminated);
        Command<HashResult>(_ => Stash.Stash());
        Command<FetchFailed>(_ => Stash.Stash());
        Command<TriggerImmediatePoll>(_ => Stash.Stash());
    }

    private void WaitingForConnection()
    {
        Command<ConnectionEstablished>(_ =>
        {
            _logger.LogInformation("Pipeline connection established - scheduling initial polls");
            Become(Ready);
            Stash.UnstashAll();
        });
        Command<OfferFailed>(msg =>
        {
            _logger.LogWarning("Initial offer failed for {Location}/{Model}: {Error} - retrying",
                msg.Location, msg.ModelId, msg.Error.Message);
            Self.Tell(new ScheduledPoll(msg.Location, msg.ModelId));
            Become(Connecting);
        });
        Command<Terminated>(OnTerminated);
        StashKnownCommands();
    }

    private void Ready()
    {
        Command<PipelineSinkResponse>(_ => { });
        Command<PipelineSourceResponse>(_ => { });
        Command<ScheduledPoll>(OnScheduledPoll);
        Command<HashResult>(OnHashResult);
        Command<FetchFailed>(OnFetchFailed);
        Command<TriggerImmediatePoll>(OnTriggerImmediatePoll);
        Command<Terminated>(OnTerminated);
    }

    private void OnTerminated(Terminated msg)
    {
        _logger.LogWarning("PipelineActor terminated - waiting for new refs");
        _queue?.Complete();
        _queue = null;
        _sourceReceived = false;

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSink());
        pipelineActor.Tell(new RequestPipelineSource());

        Become(WaitingForRefs);
    }

    private void InitializeStates()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var location in _options.Locations)
        {
            foreach (var modelId in location.ResolveModels(_options.Models))
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
        var self = Self;

        response.SourceRef.Source
            .Collect(outcome => outcome is FetchOutcome.Failure, outcome => (FetchOutcome.Failure)outcome)
            .Select(f => new FetchFailed(f.Location, f.Model.Id, f.Reason, f.Detail))
            .To(Sink.ActorRef<FetchFailed>(self, new Status.Success("failure-consumer-complete"),
                ex => new Status.Failure(ex)))
            .Run(_mat);

        _sourceReceived = true;
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
                {
                    rateLimitState = rateLimitState with { NextPollUtc = now + RateLimitMinDelay };
                }

                _states[key] = rateLimitState;
                _logger.LogWarning("Fetch rate-limited for {Location}/{Model} - next poll at {Next}",
                    msg.Location, msg.ModelId, rateLimitState.NextPollUtc);
                ScheduleNext(msg.Location, msg.ModelId);
                break;

            case FetchFailureReason.ModelUnavailable:
            case FetchFailureReason.MalformedPayload:
                _states[key] = state.WithTransientFailure(now);
                _logger.LogWarning("Fetch failed for {Location}/{Model} ({Reason}: {Detail}) - retry scheduled",
                    msg.Location, msg.ModelId, msg.Reason, msg.Detail);
                ScheduleNext(msg.Location, msg.ModelId);
                break;
        }
    }

    private void OnScheduledPoll(ScheduledPoll poll)
    {
        var target = CreateTarget(poll);
        if (target is null)
            return;

        _queue!.OfferAsync(target);
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

        _logger.LogInformation("TriggerImmediatePoll: triggered {Count} polls", targets.Count);
        Sender.Tell(new TriggerPollResult(targets.Count, targets));
    }

    private void OnHashResult(HashResult result)
    {
        var key = Key(result.Location, result.ModelId);
        var now = _timeProvider.GetUtcNow();
        var state = _states.GetValueOrDefault(key, ModelPollState.Initial(now));

        if (state.LastHash != result.Hash)
        {
            var evt = new DataChanged(result.Location, result.ModelId, result.Hash, now);
            var dto = SchedulerDtoMapping.ToDto(evt);
            Persist(dto, _ =>
            {
                _states[key] = state.WithDataChange(evt.Hash, evt.Utc, _options.DiscoveryInterval);
                _healthState.SetLastSuccessfulPoll(now);
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
            _healthState.SetLastSuccessfulPoll(now);
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
        var msg = new ScheduledPoll(location, modelId);

        if (state.NextPollUtc <= now)
        {
            Self.Tell(msg);
        }
        else
        {
            Context.System.Scheduler.ScheduleTellOnceCancelable(
                state.NextPollUtc - now, Self, msg, Self);
        }
    }

    private static string Key(string location, string modelId) => $"{location}|{modelId}";
}
