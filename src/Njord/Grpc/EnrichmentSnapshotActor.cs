using Akka.Event;
using Akka.Persistence;
using Njord.Actors;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed class EnrichmentSnapshotActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 14;

    public override string PersistenceId => "enrichment-snapshot";

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private EnrichmentSnapshotState _state = EnrichmentSnapshotState.Empty;

    public EnrichmentSnapshotActor()
    {
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is EnrichmentSnapshotDto saved)
            {
                _state = EnrichmentSnapshotStateExtensions.FromPersistence(saved);
            }
        });

        Command<UpdateEnrichment>(cmd =>
        {
            var key = EnrichmentSnapshotStateExtensions.MakeKey(cmd.Location, cmd.TypeName);
            _state = _state.Apply(key, cmd.Result);

            if (_state.UpdatesSinceSnapshot >= SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
                _state = _state.ResetSnapshotCounter();
            }

            Sender.Tell(new Ack(), Self);
        });

        Command<QueryEnrichment>(query =>
        {
            var key = EnrichmentSnapshotStateExtensions.MakeKey(query.Location, query.TypeName);
            Sender.Tell(_state.GetEnrichment(key), Self);
        });

        Command<QueryAllEnrichments>(query =>
        {
            Sender.Tell(_state.GetAllEnrichments(query.Location), Self);
        });

        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Enrichment snapshot saved (seqNr {0})", success.Metadata.SequenceNr);
            if (success.Metadata.SequenceNr > 0)
            {
                DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
            }
        });

        Command<SaveSnapshotFailure>(failure =>
        {
            _log.Warning("Enrichment snapshot save failed: {0}", failure.Cause.Message);
        });

        Command<DeleteSnapshotsSuccess>(_ => { });
        Command<DeleteSnapshotsFailure>(failure =>
        {
            _log.Warning("Old snapshot cleanup failed: {0}", failure.Cause.Message);
        });
    }
}
