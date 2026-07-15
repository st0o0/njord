using Akka.Event;
using Akka.Persistence;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Enrichment;

public sealed class ForecastHistoryActor : ReceivePersistentActor
{
    private readonly string _location;
    private readonly HistoryOptions _options;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly ForecastHistory _history;
    private int _eventsSinceSnapshot;

    public override string PersistenceId => $"forecast-history-{_location}";

    public ForecastHistoryActor(string location, HistoryOptions options, ResolvedParameterSet parameters, TimeProvider timeProvider)
    {
        _location = location;
        _options = options;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _history = new ForecastHistory(options.RetentionDays);

        Recover<ForecastRecord>(OnRecover);
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ForecastHistory saved)
            {
                foreach (var record in saved.Records)
                {
                    _history.Add(record);
                }
            }
        });

        Command<RecordSnapshot>(OnRecordSnapshot);
        Command<QueryHistory>(_ => Sender.Tell(new HistoryResponse(_history), Self));
        Command<SaveSnapshotSuccess>(_ => { });
        Command<SaveSnapshotFailure>(fail =>
        Context.GetLogger().Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
    }

    private void OnRecover(ForecastRecord evt)
    {
        var cutoff = _timeProvider.GetUtcNow().AddDays(-_options.RetentionDays);
        if (evt.Timestamp < cutoff)
        {
            return;
        }

        _history.Add(evt);
    }

    private void OnRecordSnapshot(RecordSnapshot msg)
    {
        var snapshot = msg.Snapshot;
        var now = _timeProvider.GetUtcNow();

        var modelValues = new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>();
        var consensusValues = new Dictionary<string, double?>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != _location)
            {
                continue;
            }

            var values = new Dictionary<string, double?>();
            var nearestPoint = forecast.Hourly.Points
                .OrderBy(p => Math.Abs((p.ValidAt - now).TotalMinutes))
                .FirstOrDefault();

            if (nearestPoint is not null)
            {
                foreach (var param in _parameters.Hourly)
                {
                    values[param.ApiName] = nearestPoint.Get(param);
                }
            }

            modelValues[key.Model] = values;
        }

        if (modelValues.Count > 0)
        {
            foreach (var param in _parameters.Hourly)
            {
                var vals = modelValues.Values
                    .Select(v => v.TryGetValue(param.ApiName, out var val) ? val : null)
                    .ToList();
                consensusValues[param.ApiName] = ConsensusComputer.ComputeMedian(vals);
            }
        }

        var evt = new ForecastRecord(now, _location, modelValues, consensusValues);
        Persist(evt, persisted =>
        {
            _history.Add(persisted);

            _eventsSinceSnapshot++;
            if (_eventsSinceSnapshot >= _options.SnapshotInterval)
            {
                SaveSnapshot(_history);
                _eventsSinceSnapshot = 0;
            }
        });
    }
}
