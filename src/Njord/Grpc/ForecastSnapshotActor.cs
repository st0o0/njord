using Akka.Event;
using Akka.Persistence;
using Njord.Domain.Weather;

namespace Njord.Grpc;

public sealed class ForecastSnapshotActor : ReceivePersistentActor
{
    public override string PersistenceId => "forecast-snapshot";

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly Dictionary<string, ModelForecast> _state = new();

    public ForecastSnapshotActor()
    {
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ForecastSnapshotState saved)
            {
                foreach (var kvp in saved.Forecasts)
                    _state[kvp.Key] = kvp.Value;
            }
        });

        Command<UpdateForecast>(cmd =>
        {
            var key = MakeKey(cmd.Location, cmd.Model.Id);
            _state[key] = cmd.Forecast;
            SaveSnapshot(new ForecastSnapshotState { Forecasts = new Dictionary<string, ModelForecast>(_state) });
            Sender.Tell(new Ack(), Self);
        });

        Command<GetForecast>(query =>
        {
            var key = MakeKey(query.Location, query.ModelId);
            _state.TryGetValue(key, out var forecast);
            Sender.Tell(new ForecastResponse(forecast), Self);
        });

        Command<GetAllForecasts>(_ =>
        {
            var result = _state.ToDictionary(
                kvp => ParseKey(kvp.Key),
                kvp => kvp.Value);
            Sender.Tell(new AllForecastsResponse(result), Self);
        });

        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Forecast snapshot saved (seqNr {0})", success.Metadata.SequenceNr);
        });

        Command<SaveSnapshotFailure>(failure =>
        {
            _log.Warning("Forecast snapshot save failed: {0}", failure.Cause.Message);
        });
    }

    private static string MakeKey(string location, string modelId) => $"{location}|{modelId}";

    private static (string Location, string ModelId) ParseKey(string key)
    {
        var sep = key.IndexOf('|');
        return (key[..sep], key[(sep + 1)..]);
    }

    [Serializable]
    private sealed class ForecastSnapshotState
    {
        public Dictionary<string, ModelForecast> Forecasts { get; set; } = new();
    }
}
