namespace Njord.Core.Configuration.Polling;

public static class PollingOptionsExtensions
{
    public static BudgetAllowance ToBudgetAllowance(this PollingOptions polling) =>
        new(RequestBudget.OpenMeteoFreeTierDailyLimit, BudgetCalculator.GetEffectiveBudget(polling).RequestsPerMonth);
}
