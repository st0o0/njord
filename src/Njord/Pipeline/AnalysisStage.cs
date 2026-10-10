using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Pipeline;
using Njord.Derived;
using Njord.History;
using Njord.Indices;
using Njord.Messages.Egress;
using Njord.Messages.Enrichment;
using Njord.Messages.Sensors;
using Njord.Sensors;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class AnalysisStage : IAnalysisStage
{
    private readonly ConsensusSnapshotFactory _consensusFactory;
    private readonly AlertAnalyzer _alerts;
    private readonly DerivedAnalyzer _derived;
    private readonly TrendAnalyzer _trends;
    private readonly IndexAnalyzer _indices;
    private readonly HistoryTracker _history;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly IReadOnlyList<string> _locations;
    private readonly TimeProvider _timeProvider;

    public AnalysisStage(
        ConsensusSnapshotFactory consensusFactory,
        AlertAnalyzer alerts,
        DerivedAnalyzer derived,
        TrendAnalyzer trends,
        IndexAnalyzer indices,
        HistoryTracker history,
        ResolvedParameterSet parameters,
        IOptions<NjordOptions> options,
        TimeProvider timeProvider)
    {
        _consensusFactory = consensusFactory;
        _alerts = alerts;
        _derived = derived;
        _trends = trends;
        _indices = indices;
        _history = history;
        _parameters = parameters;
        _horizons = [.. options.Value.Horizons];
        _locations = options.Value.Locations.Select(l => l.Name).ToList();
        _timeProvider = timeProvider;
    }

    public Flow<ModelForecast, IReadOnlyList<PipelineEvent>, NotUsed> CreateFlow(IUntypedActorContext context)
    {
        var historyRegion = context.GetActor<IForecastHistoryRegion>();
        var sensorHub = context.GetActor<ISensorHubActor>();

        var snapshot = ModelSnapshot.Empty;
        ConsensusSnapshot? previous = null;

        return Flow.Create<ModelForecast>()
            .SelectAsync(1, async forecast =>
            {
                snapshot = snapshot.Update(forecast);
                var location = forecast.Location;

                var events = new List<PipelineEvent>
                {
                    new PipelineEvent.ModelForecastUpdate(location, forecast.Model, forecast)
                };

                if (!snapshot.HasChanged)
                    return (IReadOnlyList<PipelineEvent>)events;

                var consensus = _consensusFactory.Create(snapshot, location);
                var sensors = await QuerySensors(sensorHub, location);

                var pollContext = new PollContext(
                    consensus, previous, sensors, snapshot, location, _horizons);

                if (_alerts.Enabled)
                {
                    var result = _alerts.Compute(pollContext);
                    if (result is not null)
                        events.Add(new PipelineEvent.AnalysisResult(location, "alerts", result, _timeProvider.GetUtcNow()));
                }

                if (_derived.Enabled)
                {
                    var result = _derived.Compute(pollContext);
                    if (result is not null)
                        events.Add(new PipelineEvent.AnalysisResult(location, "derived", result, _timeProvider.GetUtcNow()));
                }

                if (_trends.Enabled)
                {
                    var result = _trends.Compute(pollContext);
                    if (result is not null)
                        events.Add(new PipelineEvent.AnalysisResult(location, "trends", result, _timeProvider.GetUtcNow()));
                }

                if (_indices.Enabled)
                {
                    var result = _indices.Compute(pollContext);
                    if (result is not null)
                        events.Add(new PipelineEvent.AnalysisResult(location, "indices", result, _timeProvider.GetUtcNow()));
                }

                if (_history.Enabled)
                {
                    var historyResult = await RunHistory(historyRegion, snapshot, location);
                    if (historyResult is not null)
                        events.Add(new PipelineEvent.AnalysisResult(location, "history", historyResult, _timeProvider.GetUtcNow()));
                }

                events.Add(new PipelineEvent.AnalysisResult(location, "consensus", consensus, _timeProvider.GetUtcNow()));

                previous = consensus;
                return events;
            });
    }

    private static async Task<SensorSnapshot?> QuerySensors(IActorRef sensorHub, string location)
    {
        try
        {
            var response = await sensorHub.Ask<QuerySensorSnapshotResponse>(
                new QuerySensorSnapshot(location), TimeSpan.FromSeconds(3));
            return response is QuerySensorSnapshotCompleted completed ? completed.Snapshot : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<HistoryResult?> RunHistory(IActorRef historyRegion, ModelSnapshot snapshot, string location)
    {
        try
        {
            await historyRegion.Ask<Njord.Messages.Common.Ack>(
                new RecordSnapshot(location, snapshot), TimeSpan.FromSeconds(5));

            var queryResponse = await historyRegion.Ask<QueryHistoryResponse>(
                new QueryHistory(location), TimeSpan.FromSeconds(5));

            if (queryResponse is QueryHistoryCompleted completed)
                return _history.Analyze(completed.History, snapshot, location, _parameters, _timeProvider);
        }
        catch
        {
            // History failure is non-fatal
        }

        return null;
    }
}
