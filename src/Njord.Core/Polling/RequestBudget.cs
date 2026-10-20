namespace Njord.Core.Polling;

public sealed record RequestBudget(int RequestsPerMonth, int RequestsPerMinute, int DailyLimit = 10_000)
{
    public static RequestBudget OpenMeteoFreeTier { get; } = new(300_000, 600);
}
