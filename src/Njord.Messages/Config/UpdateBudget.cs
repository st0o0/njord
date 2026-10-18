namespace Njord.Messages.Config;

// Both values null clears the budget override; one value null keeps the current effective value for it.
public sealed record UpdateBudget(int? RequestsPerMonth, int? RequestsPerMinute);

public abstract record UpdateBudgetResponse;
public sealed record UpdateBudgetCompleted(EffectiveConfig Config, IReadOnlyList<string> RestartRequired)
    : UpdateBudgetResponse;
public sealed record UpdateBudgetRejected(string Reason) : UpdateBudgetResponse;
public sealed record UpdateBudgetFailed(Exception Cause) : UpdateBudgetResponse;
