namespace Njord.Messages.Pipeline;

public sealed record QueryBudgetUsage;

public abstract record QueryBudgetUsageResponse;
public sealed record QueryBudgetUsageCompleted(long MonthlyUsed, long DailyUsed) : QueryBudgetUsageResponse;

