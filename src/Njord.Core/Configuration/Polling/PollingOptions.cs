namespace Njord.Core.Configuration.Polling;

public sealed class PollingOptions
{
    public const string SectionName = "Njord";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(60);

    public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromMinutes(20);

    public RequestBudget? BudgetOverride { get; set; }
}
