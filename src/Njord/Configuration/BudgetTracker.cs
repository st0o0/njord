namespace Njord.Configuration;

public sealed class BudgetTracker
{
    private long _monthlyUsed;
    private long _dailyUsed;
    private int _currentMonth;
    private int _currentDay;
    private readonly object _lock = new();

    public BudgetTracker()
    {
        var now = DateTime.UtcNow;
        _currentMonth = now.Month;
        _currentDay = now.DayOfYear;
    }

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
        var now = DateTime.UtcNow;
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
