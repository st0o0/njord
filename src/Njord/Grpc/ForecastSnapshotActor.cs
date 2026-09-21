using Akka.Event;
using Akka.Persistence;
using Njord.Actors;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed class ForecastSnapshotActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 20;

    public override string PersistenceId => "forecast-snapshot";

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private ForecastSnapshotState _state = ForecastSnapshotState.Empty;

    public ForecastSnapshotActor()
    {
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ForecastSnapshotDto saved)
            {
                _state = ForecastSnapshotStateExtensions.FromPersistence(saved);
            }
        });

        Command<UpdateForecast>(cmd =>
        {
            var key = ForecastSnapshotStateExtensions.MakeKey(cmd.Location, cmd.Model.Id);
            _state = _state.Apply(key, cmd.Forecast);

            if (_state.UpdatesSinceSnapshot >= SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
                _state = _state.ResetSnapshotCounter();
            }

            Sender.Tell(new Ack(), Self);
        });

        Command<QueryForecast>(query =>
        {
            var key = ForecastSnapshotStateExtensions.MakeKey(query.Location, query.ModelId);
            Sender.Tell(_state.GetForecast(key), Self);
        });

        Command<QueryAllForecasts>(_ =>
        {
            Sender.Tell(_state.GetAllForecasts(), Self);
        });

        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Forecast snapshot saved (seqNr {0})", success.Metadata.SequenceNr);
            if (success.Metadata.SequenceNr > 0)
            {
                DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
            }
        });

        Command<SaveSnapshotFailure>(failure =>
        {
            _log.Warning("Forecast snapshot save failed: {0}", failure.Cause.Message);
        });

        Command<DeleteSnapshotsSuccess>(_ => { });
        Command<DeleteSnapshotsFailure>(failure =>
        {
            _log.Warning("Old snapshot cleanup failed: {0}", failure.Cause.Message);
        });
    }
}
