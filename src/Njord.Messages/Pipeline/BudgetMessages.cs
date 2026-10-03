namespace Njord.Messages.Pipeline;

public abstract record QueryBudgetUsageResponse;
public sealed record QueryBudgetUsageResult(long MonthlyUsed, long DailyUsed) : QueryBudgetUsageResponse;
public sealed record QueryBudgetUsageFailed(Exception Cause) : QueryBudgetUsageResponse;

public sealed record RecordApiCall(int Weight);
public sealed record QueryBudgetUsage;
