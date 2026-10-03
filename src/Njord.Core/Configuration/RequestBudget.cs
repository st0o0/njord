namespace Njord.Configuration;

public sealed record RequestBudget(int RequestsPerMonth, int RequestsPerMinute)
{
    public static RequestBudget OpenMeteoFreeTier { get; } = new(300_000, 600);

    public const int OpenMeteoFreeTierDailyLimit = 10_000;
}
