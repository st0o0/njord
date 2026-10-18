using Njord.Core.Health;

namespace Njord.Core.Configuration.Polling;

public static class PollingOptionsExtensions
{
    public static BudgetHealthSnapshot ToInitialBudgetSnapshot(this PollingOptions polling) =>
        new(0, 0, RequestBudget.OpenMeteoFreeTierDailyLimit, BudgetCalculator.GetEffectiveBudget(polling).RequestsPerMonth);
}
