using Akka.Event;
using Akka.Persistence;
using Njord.Core.Health;
using Njord.Messages.Budget;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Budget;

namespace Njord.Budget;

public sealed class BudgetTracker : ReceivePersistentActor
{
    private const int SnapshotInterval = 50;

    public override string PersistenceId => "budget-tracker";

    private readonly TimeProvider _timeProvider;
    private readonly NjordHealthMonitor _healthState;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private BudgetTrackerState _state;

    public BudgetTracker(TimeProvider timeProvider, NjordHealthMonitor healthState)
    {
        _timeProvider = timeProvider;
        _healthState = healthState;

        var now = timeProvider.GetUtcNow();
        _state = BudgetTrackerState.Empty with { CurrentMonth = now.Month, CurrentDay = now.DayOfYear };

        Recover<ApiCallRecorded>(dto =>
        {
            var (weight, utc) = BudgetTrackerStateExtensions.FromPersistedEvent(dto);
            _state = _state.ApplyRecover(weight, utc, _timeProvider.GetUtcNow());
        });
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is PersistedBudgetTrackerState snapshot)
            {
                _state = BudgetTrackerState.FromPersistence(snapshot, _timeProvider.GetUtcNow());
            }
        });

        Command<RecordApiCall>(OnRecordApiCall);
        Command<QueryBudgetUsage>(_ => Sender.Tell(_state.GetSnapshot(), Self));
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

    // The limits are the health snapshot's, which the configuration manager keeps at the effective budget.
    private BudgetStatusResult GetStatus()
    {
        var limits = _healthState.Budget;
        return new BudgetStatusResult(_state.MonthlyUsed, _state.DailyUsed, limits.LimitMonthly, limits.LimitDaily);
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
            var limits = _healthState.Budget;
            _healthState.UpdateBudget(new BudgetHealthSnapshot(
                _state.DailyUsed, _state.MonthlyUsed,
                limits.LimitDaily, limits.LimitMonthly));

            MaybeSnapshot();
        });
    }
}
