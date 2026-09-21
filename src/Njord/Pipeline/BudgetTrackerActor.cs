using Akka.Event;
using Akka.Persistence;
using Njord.Health;
using Njord.Persistence;

namespace Njord.Pipeline;

public abstract record BudgetResponse;
public sealed record BudgetUsageResult(long MonthlyUsed, long DailyUsed) : BudgetResponse;
public sealed record BudgetResponseFailed(Exception Cause) : BudgetResponse;

public sealed class BudgetTrackerActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 50;

    public override string PersistenceId => "budget-tracker";

    public sealed record RecordApiCall(int Weight);
    public sealed record QueryBudgetUsage;

    private readonly TimeProvider _timeProvider;
    private readonly NjordHealthState _healthState;
    private BudgetTrackerState _state;

    public BudgetTrackerActor(TimeProvider timeProvider, NjordHealthState healthState)
    {
        _timeProvider = timeProvider;
        _healthState = healthState;
        _state = BudgetTrackerState.Empty(timeProvider);

        Recover<ApiCallRecordedDto>(dto =>
        {
            var (weight, utc) = BudgetTrackerDtoMapping.ToDomain(dto);
            _state = _state.ApplyRecover(weight, utc, _timeProvider.GetUtcNow());
        });
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is BudgetTrackerSnapshotDto snapshot)
            {
                _state = BudgetTrackerStateExtensions.FromPersistence(snapshot, _timeProvider.GetUtcNow());
            }
        });

        Command<RecordApiCall>(OnRecordApiCall);
        Command<QueryBudgetUsage>(_ => Sender.Tell(_state.GetSnapshot(), Self));
        Command<SaveSnapshotSuccess>(success =>
        {
            DeleteMessages(success.Metadata.SequenceNr);
            DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
        });
        Command<SaveSnapshotFailure>(fail =>
            Context.GetLogger()
                .Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteSnapshotSuccess>(_ => { });
    }

    private void OnRecordApiCall(RecordApiCall cmd)
    {
        var now = _timeProvider.GetUtcNow();
        var dto = BudgetTrackerDtoMapping.ToDto(cmd.Weight, now);

        Persist(dto, _ =>
        {
            _state = _state.Apply(cmd.Weight, now);
            _healthState.SetBudgetUsage(_state.DailyUsed, _state.MonthlyUsed);

            if (_state.EventsSinceSnapshot >= SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
                _state = _state with { EventsSinceSnapshot = 0 };
            }
        });
    }
}
