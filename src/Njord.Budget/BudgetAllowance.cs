using Njord.Core.Configuration;

namespace Njord.Budget;

public sealed record BudgetAllowance(long Daily, long Monthly)
{
    public static BudgetAllowance From(PollingOptions polling) =>
        new(polling.EffectiveBudget.DailyLimit, polling.EffectiveBudget.RequestsPerMonth);
}
