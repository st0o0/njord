namespace Njord.Messages.Budget;

public sealed record QueryBudgetStatus;

public sealed record BudgetStatusResult(long MonthlyUsed, long DailyUsed, long MonthlyLimit, long DailyLimit);
