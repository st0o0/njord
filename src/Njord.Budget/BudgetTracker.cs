using Akka.Event;
using Akka.Persistence;
using Njord.Budget.Health;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Budget;

namespace Njord.Budget;

public sealed class BudgetTracker : ReceivePersistentActor
{
    private const int SnapshotInterval = 50;

    public override string PersistenceId => "budget-tracker";

    private readonly TimeProvider _timeProvider;
    private readonly BudgetUsageState _usage;
    private readonly ConfigView _config;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private BudgetTrackerState _state;

    public BudgetTracker(TimeProvider timeProvider, BudgetUsageState usage, ConfigView config)
    {
        _timeProvider = timeProvider;
        _usage = usage;
        _config = config;

        var now = timeProvider.GetUtcNow();
        _state = BudgetTrackerState.Empty with { CurrentMonth = now.Month, CurrentDay = now.DayOfYear };

        Recover<ApiCallRecorded>(dto =>
        {
            var (weight, utc) = dto.FromPersistedEvent();
            _state = _state.ApplyRecover(weight, utc, _timeProvider.GetUtcNow());
        });
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is PersistedBudgetTrackerState snapshot)
            {
                _state = snapshot.FromPersistence(_timeProvider.GetUtcNow());
            }
        });

        Recover<RecoveryCompleted>(_ => _usage.Update(_state.DailyUsed, _state.MonthlyUsed));

        Command<RecordApiCall>(OnRecordApiCall);
        Command<QueryBudgetUsage>(_ => Sender.Tell(_state.ToBudgetUsage(), Self));
        Command<QueryBudgetStatus>(_ => Sender.Tell(GetStatus(), Self));
        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Snapshot saved at sequence {SequenceNr}, cleaning journal", success.Metadata.SequenceNr);
            DeleteMessages(success.Metadata.SequenceNr);
            DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
        });
        Command<SaveSnapshotFailure>(fail =>
            _log.Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesFailure>(fail =>
            _log.Warning(fail.Cause, "Delete messages failed for {PersistenceId}", PersistenceId));
        Command<DeleteSnapshotsFailure>(fail =>
            _log.Warning(fail.Cause, "Delete snapshots failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteSnapshotSuccess>(_ => { });
    }

    private BudgetStatusResult GetStatus()
    {
        var limits = _config.Polling.ToBudgetAllowance();
        return new BudgetStatusResult(_state.MonthlyUsed, _state.DailyUsed, limits.Monthly, limits.Daily);
    }

    private void MaybeSnapshot()
    {
        if (LastSequenceNr % SnapshotInterval == 0)
            SaveSnapshot(_state.GetPersistenceState());
    }

    private void OnRecordApiCall(RecordApiCall cmd)
    {
        var now = _timeProvider.GetUtcNow();
        var (newState, evt) = _state.ProcessCommand(cmd, now);

        Persist(evt, _ =>
        {
            _state = newState;
            _usage.Update(_state.DailyUsed, _state.MonthlyUsed);

            MaybeSnapshot();
        });
    }
}
