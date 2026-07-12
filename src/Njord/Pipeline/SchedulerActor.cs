using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Pipeline;

public sealed class SchedulerActor : ReceivePersistentActor
{
    public override string PersistenceId => "scheduler";

    private readonly NjordOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SchedulerActor> _logger;
    private readonly ActorRegistry _registry;
    private readonly Dictionary<string, ModelPollState> _states = new();
    private ISourceQueueWithComplete<WeightedTarget>? _queue;
    private int _weight;

    public sealed record DataChanged(string Location, string ModelId, int Hash, DateTimeOffset Utc);

    public SchedulerActor(
        IOptions<NjordOptions> options,
        TimeProvider timeProvider,
        ILogger<SchedulerActor> logger,
        ResolvedParameterSet parameters,
        ActorRegistry registry)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _registry = registry;
        _weight = WeightedTarget.ComputeWeight(parameters.HourlyCount, _options.ForecastDays);

        Recover<DataChanged>(OnRecover);
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
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSink());
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

        _logger.LogInformation("Pipeline SinkRef received — scheduling initial polls");
        InitializeStates();
        BecomeReady();
        Stash.UnstashAll();
    }

    private void BecomeReady()
    {
        Command<PipelineSinkResponse>(_ => { });
        Command<ScheduledPoll>(OnScheduledPoll);
        Command<HashResult>(OnHashResult);
        Command<PipelineCommand.RefreshModel>(OnRefreshModel);
        Command<PipelineCommand.RefreshLocation>(OnRefreshLocation);
        Command<Terminated>(_ =>
        {
            _logger.LogWarning("PipelineActor terminated — waiting for new SinkRef");
            _queue?.Complete();
            _queue = null;
            var pipelineActor = _registry.Get<PipelineActor>();
            Context.Watch(pipelineActor);
            pipelineActor.Tell(new RequestPipelineSink());
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
                    _states[key] = ModelPollState.Initial(now);

                ScheduleNext(location.Name, modelId);
            }
        }
    }

    private void OnScheduledPoll(ScheduledPoll poll)
    {
        if (_queue is null) return;

        var location = _options.Locations.FirstOrDefault(l =>
            l.Name.Equals(poll.Location, StringComparison.OrdinalIgnoreCase));
        if (location is null) return;

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
                    "Data changed for {Location}/{Model} — phase={Phase}, cycle={Cycle}",
                    result.Location, result.ModelId, _states[key].Phase, _states[key].Cycle);
                ScheduleNext(result.Location, result.ModelId);
                Sender.Tell(new Ack());
            });
        }
        else
        {
            _states[key] = state.WithMiss(now, _options.DiscoveryInterval);
            _logger.LogDebug(
                "No data change for {Location}/{Model} — miss={Miss}, phase={Phase}",
                result.Location, result.ModelId, _states[key].MissCount, _states[key].Phase);
            ScheduleNext(result.Location, result.ModelId);
            Sender.Tell(new Ack());
        }
    }

    private void OnRefreshModel(PipelineCommand.RefreshModel cmd)
    {
        if (_queue is null) return;

        var location = _options.Locations.FirstOrDefault(l =>
            l.Name.Equals(cmd.Location, StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            _logger.LogWarning("Ignoring RefreshModel for unknown location {Location}", cmd.Location);
            return;
        }

        if (!_options.Models.Contains(cmd.Model.Id, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Ignoring RefreshModel for unknown model {Model}", cmd.Model.Id);
            return;
        }

        var cycle = new CycleId(_timeProvider.GetUtcNow());
        var target = new WeightedTarget(location, cmd.Model, _weight, cycle);
        _queue.OfferAsync(target);
    }

    private void OnRefreshLocation(PipelineCommand.RefreshLocation cmd)
    {
        if (_queue is null) return;

        var location = _options.Locations.FirstOrDefault(l =>
            l.Name.Equals(cmd.Location, StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            _logger.LogWarning("Ignoring RefreshLocation for unknown location {Location}", cmd.Location);
            return;
        }

        var cycle = new CycleId(_timeProvider.GetUtcNow());
        foreach (var modelId in _options.Models)
        {
            var target = new WeightedTarget(location, new WeatherModel(modelId), _weight, cycle);
            _queue.OfferAsync(target);
        }
    }

    private void ScheduleNext(string location, string modelId)
    {
        var key = Key(location, modelId);
        if (!_states.TryGetValue(key, out var state)) return;

        var now = _timeProvider.GetUtcNow();
        var delay = state.NextPollUtc <= now
            ? TimeSpan.FromSeconds(1)
            : state.NextPollUtc - now;

        Context.System.Scheduler.ScheduleTellOnce(
            delay, Self, new ScheduledPoll(location, modelId), Self);
    }

    private static string Key(string location, string modelId) => $"{location}|{modelId}";
}
