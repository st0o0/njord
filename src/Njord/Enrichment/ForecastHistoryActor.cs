using Akka.Event;
using Akka.Persistence;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Persistence;

namespace Njord.Enrichment;

public sealed class ForecastHistoryActor : ReceivePersistentActor
{
    private readonly string _location;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private ForecastHistoryState _state;

    public override string PersistenceId => $"forecast-history-{_location}";

    public ForecastHistoryActor(string location, HistoryOptions options, ResolvedParameterSet parameters, TimeProvider timeProvider)
    {
        _location = location;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _state = ForecastHistoryState.Create(options.RetentionDays, options.SnapshotInterval);

        Recover<ForecastRecordDto>(dto =>
        {
            var evt = ForecastHistoryDtoMapping.ToDomain(dto);
            var cutoff = _timeProvider.GetUtcNow().AddDays(-options.RetentionDays);
            _state = _state.ApplyRecover(evt, cutoff);
        });
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ForecastHistorySnapshotDto saved)
            {
                _state = ForecastHistoryStateExtensions.FromPersistence(saved, options.SnapshotInterval);
            }
        });

        Command<RecordSnapshot>(OnRecordSnapshot);
        Command<QueryHistory>(_ => Sender.Tell(_state.GetSnapshot(), Self));
        Command<SaveSnapshotSuccess>(success =>
        {
            DeleteMessages(success.Metadata.SequenceNr);
            DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
        });
        Command<SaveSnapshotFailure>(fail =>
            Context.GetLogger()
                .Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteSnapshotSuccess>(_ => { });
    }

    private void OnRecordSnapshot(RecordSnapshot msg)
    {
        var snapshot = msg.Snapshot;
        var now = _timeProvider.GetUtcNow();

        var consensusValues = new Dictionary<string, double?>();
        var modelValuesList = new List<IReadOnlyDictionary<string, double?>>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != _location)
                continue;

            var values = new Dictionary<string, double?>();
            var nearestPoint = forecast.Hourly.Points
                .OrderBy(p => Math.Abs((p.ValidAt - now).TotalMinutes))
                .FirstOrDefault();

            if (nearestPoint is not null)
            {
                foreach (var param in _parameters.Hourly)
                    values[param.ApiName] = nearestPoint.Get(param);
            }

            modelValuesList.Add(values);
        }

        if (modelValuesList.Count > 0)
        {
            foreach (var param in _parameters.Hourly)
            {
                var vals = modelValuesList
                    .Select(v => v.TryGetValue(param.ApiName, out var val) ? val : null)
                    .ToList();
                consensusValues[param.ApiName] = ConsensusComputer.ComputeMedian(vals);
            }
        }

        var emptyModelValues = new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>();
        var evt = new ForecastRecord(now, _location, emptyModelValues, consensusValues);
        var dto = ForecastHistoryDtoMapping.ToDto(evt);
        Persist(dto, _ =>
        {
            _state = _state.Apply(evt);

            if (_state.EventsSinceSnapshot >= _state.SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
                _state = _state.ResetSnapshotCounter();
            }
        });
    }
}
