namespace Njord.Configuration;

public sealed class BudgetTracker(TimeProvider timeProvider)
{
    private long _monthlyUsed;
    private long _dailyUsed;
    private int _currentMonth = timeProvider.GetUtcNow().Month;
    private int _currentDay = timeProvider.GetUtcNow().DayOfYear;
    private readonly object _lock = new();

    public void RecordCall(int weight = 1)
    {
        lock (_lock)
        {
            ResetIfNeeded();
            _monthlyUsed += weight;
            _dailyUsed += weight;
        }
    }

    public (long MonthlyUsed, long DailyUsed) GetUsage()
    {
        lock (_lock)
        {
            ResetIfNeeded();
            return (_monthlyUsed, _dailyUsed);
        }
    }

    private void ResetIfNeeded()
    {
        var now = timeProvider.GetUtcNow();
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
