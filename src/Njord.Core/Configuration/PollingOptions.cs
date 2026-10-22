using Njord.Core.Polling;

namespace Njord.Core.Configuration;

public sealed class PollingOptions : IConfigSection
{
    private const string SectionName = "Njord:Polling";
    static string IConfigSection.SectionName => SectionName;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromMinutes(20);

    public RequestBudget? BudgetOverride { get; set; }

    public RequestBudget EffectiveBudget => BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;
}
