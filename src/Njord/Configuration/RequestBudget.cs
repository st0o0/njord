namespace Njord.Configuration;

public sealed record RequestBudget(int RequestsPerMonth, int RequestsPerMinute)
{
    /// <summary>Open-Meteo free-tier soft limits (verified 2026-07-11). Not a contract — stay polite.</summary>
    public static RequestBudget OpenMeteoFreeTier { get; } = new(300_000, 600);
}
