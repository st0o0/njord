using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Persistence;
using Njord.Domain.Weather;
using Njord.Messages.Common;
using Njord.Messages.Snapshots;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed class ForecastSnapshotActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 20;

    private readonly string _entityId;
    public override string PersistenceId { get; }

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private ModelForecast? _forecast;
    private int _updatesSinceSnapshot;

    public ForecastSnapshotActor(string entityId)
    {
        _entityId = entityId;
        PersistenceId = $"forecast-snapshot-{entityId}";

        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ForecastSnapshotDto saved)
            {
                var domain = ForecastSnapshotMapping.ToDomain(saved);
                if (domain.TryGetValue(_entityId, out var forecast))
                {
                    _forecast = forecast;
                }
            }
        });

        Command<UpdateForecast>(cmd =>
        {
            _forecast = cmd.Forecast;
            _updatesSinceSnapshot++;

            if (_updatesSinceSnapshot >= SnapshotInterval)
            {
                var dto = ForecastSnapshotMapping.ToDto(
                    new Dictionary<string, ModelForecast> { [_entityId] = _forecast });
                SaveSnapshot(dto);
                _updatesSinceSnapshot = 0;
            }

            Sender.Tell(new Ack(), Self);
        });

        Command<QueryForecast>(query =>
        {
            Sender.Tell(
                _forecast is not null
                    ? new ForecastFound(_forecast)
                    : (QueryForecastResponse)new ForecastNotFound(_entityId),
                Self);
        });

        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Forecast snapshot saved (seqNr {0}) for {1}", success.Metadata.SequenceNr, _entityId);
            if (success.Metadata.SequenceNr > 0)
            {
                DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
            }
        });

        Command<SaveSnapshotFailure>(failure =>
        {
            _log.Warning("Forecast snapshot save failed for {0}: {1}", _entityId, failure.Cause.Message);
        });

        Command<DeleteSnapshotsSuccess>(_ => { });
        Command<DeleteSnapshotsFailure>(failure =>
        {
            _log.Warning("Old snapshot cleanup failed for {0}: {1}", _entityId, failure.Cause.Message);
        });
    }
}
