namespace Njord.Messages.Pipeline;

public abstract record BudgetResponse;
public sealed record BudgetUsageResult(long MonthlyUsed, long DailyUsed) : BudgetResponse;
public sealed record BudgetResponseFailed(Exception Cause) : BudgetResponse;

public sealed record RecordApiCall(int Weight);
public sealed record QueryBudgetUsage;
