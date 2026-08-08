using Akka.Event;
using Akka.Persistence;
using Njord.Health;
using Njord.Persistence;

namespace Njord.Pipeline;

public sealed class BudgetTrackerActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 50;

    public override string PersistenceId => "budget-tracker";

    public sealed record RecordApiCall(int Weight);
    public sealed record GetBudgetUsage;
    public sealed record BudgetUsage(long MonthlyUsed, long DailyUsed);

    private readonly TimeProvider _timeProvider;
    private readonly NjordHealthState _healthState;
    private int _currentMonth;
    private int _currentDay;
    private long _monthlyUsed;
    private long _dailyUsed;
    private int _eventsSinceSnapshot;

    public BudgetTrackerActor(TimeProvider timeProvider, NjordHealthState healthState)
    {
        _timeProvider = timeProvider;
        _healthState = healthState;
        var now = timeProvider.GetUtcNow();
        _currentMonth = now.Month;
        _currentDay = now.DayOfYear;

        Recover<ApiCallRecordedDto>(OnRecover);
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is BudgetTrackerSnapshotDto snapshot)
            {
                RestoreFromSnapshot(snapshot);
            }
        });

        Command<RecordApiCall>(OnRecordApiCall);
        Command<GetBudgetUsage>(_ => Sender.Tell(new BudgetUsage(_monthlyUsed, _dailyUsed), Self));
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

    private void OnRecover(ApiCallRecordedDto dto)
    {
        var (weight, utc) = BudgetTrackerDtoMapping.ToDomain(dto);
        var now = _timeProvider.GetUtcNow();

        if (utc.Month != now.Month || utc.Year != now.Year)
        {
            return;
        }

        _monthlyUsed += weight;

        if (utc.DayOfYear == now.DayOfYear)
        {
            _dailyUsed += weight;
        }
    }

    private void RestoreFromSnapshot(BudgetTrackerSnapshotDto snapshot)
    {
        var now = _timeProvider.GetUtcNow();

        if (snapshot.Month != now.Month)
        {
            _monthlyUsed = 0;
            _dailyUsed = 0;
        }
        else if (snapshot.Day != now.DayOfYear)
        {
            _monthlyUsed = snapshot.MonthlyUsed;
            _dailyUsed = 0;
        }
        else
        {
            _monthlyUsed = snapshot.MonthlyUsed;
            _dailyUsed = snapshot.DailyUsed;
        }

        _currentMonth = now.Month;
        _currentDay = now.DayOfYear;
    }

    private void OnRecordApiCall(RecordApiCall cmd)
    {
        ResetIfNeeded();

        var now = _timeProvider.GetUtcNow();
        var dto = BudgetTrackerDtoMapping.ToDto(cmd.Weight, now);

        Persist(dto, _ =>
        {
            _monthlyUsed += cmd.Weight;
            _dailyUsed += cmd.Weight;
            _healthState.SetBudgetUsage(_dailyUsed, _monthlyUsed);

            _eventsSinceSnapshot++;
            if (_eventsSinceSnapshot >= SnapshotInterval)
            {
                SaveSnapshot(BudgetTrackerDtoMapping.ToSnapshot(
                    _currentMonth, _currentDay, _monthlyUsed, _dailyUsed));
                _eventsSinceSnapshot = 0;
            }
        });
    }

    private void ResetIfNeeded()
    {
        var now = _timeProvider.GetUtcNow();
        if (now.Month != _currentMonth)
        {
            _monthlyUsed = 0;
            _dailyUsed = 0;
            _currentMonth = now.Month;
            _currentDay = now.DayOfYear;
        }
        else if (now.DayOfYear != _currentDay)
        {
            _dailyUsed = 0;
            _currentDay = now.DayOfYear;
        }
    }
}
