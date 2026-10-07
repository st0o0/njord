using Akka.Event;
using Akka.Persistence;
using Njord.Messages.Common;
using Njord.Messages.Snapshots;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed class EnrichmentSnapshotActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 14;

    private readonly string _entityId;
    public override string PersistenceId { get; }

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private object? _result;
    private int _updatesSinceSnapshot;

    public EnrichmentSnapshotActor(string entityId)
    {
        _entityId = entityId;
        PersistenceId = $"enrichment-snapshot-{entityId}";

        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is EnrichmentSnapshotDto saved)
            {
                var domain = EnrichmentSnapshotMapping.ToDomain(saved);
                if (domain.TryGetValue(_entityId, out var result))
                {
                    _result = result;
                }
            }
        });

        Command<UpdateEnrichment>(cmd =>
        {
            _result = cmd.Result;
            _updatesSinceSnapshot++;

            if (_updatesSinceSnapshot >= SnapshotInterval)
            {
                var dto = EnrichmentSnapshotMapping.ToDto(
                    new Dictionary<string, object> { [_entityId] = _result });
                SaveSnapshot(dto);
                _updatesSinceSnapshot = 0;
            }

            Sender.Tell(new Ack(), Self);
        });

        Command<QueryEnrichment>(query =>
        {
            Sender.Tell(
                _result is not null
                    ? new EnrichmentFound(_result)
                    : (QueryEnrichmentResponse)new EnrichmentNotFound(_entityId),
                Self);
        });

        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Enrichment snapshot saved (seqNr {0}) for {1}", success.Metadata.SequenceNr, _entityId);
            if (success.Metadata.SequenceNr > 0)
            {
                DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
            }
        });

        Command<SaveSnapshotFailure>(failure =>
        {
            _log.Warning("Enrichment snapshot save failed for {0}: {1}", _entityId, failure.Cause.Message);
        });

        Command<DeleteSnapshotsSuccess>(_ => { });
        Command<DeleteSnapshotsFailure>(failure =>
        {
            _log.Warning("Old snapshot cleanup failed for {0}: {1}", _entityId, failure.Cause.Message);
        });
    }
}
