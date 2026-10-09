using Akka.Event;
using Akka.Persistence;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Messages.Enrichment;
using Njord.Persistence;
using Njord.Weather;

namespace Njord.Enrichment;

public sealed class ForecastHistoryActor : ReceivePersistentActor
{
    private readonly string _location;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private ForecastHistoryState _state;

    public override string PersistenceId => $"forecast-history-{_location}";

    public ForecastHistoryActor(string entityId, IOptions<EnrichmentOptions> enrichmentOptions, ResolvedParameterSet parameters, TimeProvider timeProvider)
    {
        _location = entityId;
        var historyOptions = enrichmentOptions.Value.History;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _state = ForecastHistoryState.Create(historyOptions.RetentionDays, historyOptions.SnapshotInterval);

        Recover<ForecastRecordDto>(dto =>
        {
            var evt = ForecastHistoryDtoMapping.ToDomain(dto);
            var cutoff = _timeProvider.GetUtcNow().AddDays(-historyOptions.RetentionDays);
            _state = _state.ApplyRecover(evt, cutoff);
        });
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is ForecastHistorySnapshotDto saved)
            {
                _state = ForecastHistoryStateExtensions.FromPersistence(saved, historyOptions.SnapshotInterval);
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
        Command<DeleteMessagesFailure>(fail =>
            Context.GetLogger()
                .Warning(fail.Cause, "Delete messages failed for {PersistenceId}", PersistenceId));
        Command<DeleteSnapshotsFailure>(fail =>
            Context.GetLogger()
                .Warning(fail.Cause, "Delete snapshots failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteSnapshotSuccess>(_ => { });
    }

    private void OnRecordSnapshot(RecordSnapshot msg)
    {
        var evt = _state.ProcessSnapshot(msg.Snapshot, _location, _parameters, _timeProvider.GetUtcNow());
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
