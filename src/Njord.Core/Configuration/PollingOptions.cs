using Njord.Core.Polling;

namespace Njord.Core.Configuration;

public sealed class PollingOptions : IConfigSection
{
    private const string SectionName = "Njord:Polling";
    static string IConfigSection.SectionName => SectionName;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    public Dictionary<string, DomainPollingOverride> Domains { get; set; } = [];

    public RequestBudget? BudgetOverride { get; set; }

    public RequestBudget Budget { get; set; } = RequestBudget.OpenMeteoFreeTier;

    public TimeSpan ResolvedInterval(DomainKind domain)
    {
        var domainName = domain.ToString();
        if (Domains.TryGetValue(domainName, out var over) && over.PollInterval > TimeSpan.Zero)
        {
            return over.PollInterval < PollInterval ? PollInterval : over.PollInterval;
        }

        var def = DomainDefaults.PollInterval(domain);
        return def < PollInterval ? PollInterval : def;
    }
}

public sealed class DomainPollingOverride
{
    public TimeSpan PollInterval { get; set; }
}
