namespace Njord.Core.Configuration.Polling;

public sealed class PollingOptions
{
    public const string SectionName = "Njord";

    public TimeSpan PollInterval { get; set; }

    public TimeSpan DiscoveryInterval { get; set; }

    public RequestBudget? BudgetOverride { get; set; }
}
