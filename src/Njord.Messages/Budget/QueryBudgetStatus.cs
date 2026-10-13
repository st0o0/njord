namespace Njord.Messages.Budget;

public sealed record QueryBudgetStatus;

public abstract record BudgetStatusResponse;
public sealed record BudgetStatusResult(long MonthlyUsed, long DailyUsed, long MonthlyLimit, long DailyLimit)
    : BudgetStatusResponse;
public sealed record BudgetStatusFailed(Exception Cause) : BudgetStatusResponse;
