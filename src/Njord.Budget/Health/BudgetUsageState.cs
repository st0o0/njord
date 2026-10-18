namespace Njord.Budget.Health;

public sealed record BudgetUsage(long Daily, long Monthly);

public sealed class BudgetUsageState
{
    private BudgetUsage _current = new(0, 0);

    public BudgetUsage Current => Volatile.Read(ref _current);

    public void Update(long daily, long monthly) => Volatile.Write(ref _current, new BudgetUsage(daily, monthly));
}
