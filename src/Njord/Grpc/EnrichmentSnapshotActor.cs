using Akka.Event;
using Akka.Persistence;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed class EnrichmentSnapshotActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 14;

    public override string PersistenceId => "enrichment-snapshot";

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly Dictionary<string, object> _state = new();
    private int _updatesSinceSnapshot;

    public EnrichmentSnapshotActor()
    {
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is EnrichmentSnapshotDto saved)
            {
                foreach (var kvp in EnrichmentSnapshotMapping.ToDomain(saved))
                    _state[kvp.Key] = kvp.Value;
            }
        });

        Command<UpdateEnrichment>(cmd =>
        {
            var key = MakeKey(cmd.Location, cmd.TypeName);
            _state[key] = cmd.Result;

            _updatesSinceSnapshot++;
            if (_updatesSinceSnapshot >= SnapshotInterval)
            {
                SaveSnapshot(EnrichmentSnapshotMapping.ToDto(_state));
                _updatesSinceSnapshot = 0;
            }

            Sender.Tell(new Ack(), Self);
        });

        Command<GetEnrichment>(query =>
        {
            var key = MakeKey(query.Location, query.TypeName);
            _state.TryGetValue(key, out var result);
            Sender.Tell(new EnrichmentResponse(result), Self);
        });

        Command<GetAllEnrichments>(query =>
        {
            var prefix = $"{query.Location}|";
            var results = _state
                .Where(kvp => kvp.Key.StartsWith(prefix))
                .Select(kvp => (TypeName: kvp.Key[(prefix.Length)..], kvp.Value))
                .ToList();
            Sender.Tell(new AllEnrichmentsResponse(results), Self);
        });

        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Enrichment snapshot saved (seqNr {0})", success.Metadata.SequenceNr);
            if (success.Metadata.SequenceNr > 0)
                DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
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

    private static string MakeKey(string location, string typeName) => $"{location}|{typeName}";
}
