namespace Njord.Core.Configuration.Polling;

public sealed class PollingOptions
{
    public const string SectionName = "Njord:Polling";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromMinutes(20);

    public RequestBudget? BudgetOverride { get; set; }
}
