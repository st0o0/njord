using Akka.Event;
using Akka.Persistence;

namespace Njord.Grpc;

public sealed class EnrichmentSnapshotActor : ReceivePersistentActor
{
    public override string PersistenceId => "enrichment-snapshot";

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly Dictionary<string, object> _state = new();

    public EnrichmentSnapshotActor()
    {
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is EnrichmentSnapshotState saved)
            {
                foreach (var kvp in saved.Enrichments)
                    _state[kvp.Key] = kvp.Value;
            }
        });

        Command<UpdateEnrichment>(cmd =>
        {
            var key = MakeKey(cmd.Location, cmd.TypeName);
            _state[key] = cmd.Result;
            SaveSnapshot(new EnrichmentSnapshotState { Enrichments = new Dictionary<string, object>(_state) });
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
        });

        Command<SaveSnapshotFailure>(failure =>
        {
            _log.Warning("Enrichment snapshot save failed: {0}", failure.Cause.Message);
        });
    }

    private static string MakeKey(string location, string typeName) => $"{location}|{typeName}";

    [Serializable]
    private sealed class EnrichmentSnapshotState
    {
        public Dictionary<string, object> Enrichments { get; set; } = new();
    }
}
