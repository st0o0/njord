namespace Njord.Messages.Budget;

public sealed record QueryBudgetUsage;

public abstract record BudgetUsageResponse;
public sealed record BudgetUsageResult(long MonthlyUsed, long DailyUsed) : BudgetUsageResponse;
public sealed record BudgetUsageFailed(Exception Cause) : BudgetUsageResponse;

