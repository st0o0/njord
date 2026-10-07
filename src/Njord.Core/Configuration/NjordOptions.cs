using Njord.Domain.Options;

namespace Njord.Core.Configuration;

public sealed class NjordOptions
{
    public const string SectionName = "Njord";

    public RequestBudget? BudgetOverride { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(60);

    public IList<LocationOptions> Locations { get; set; } = [];

    public IList<string> Models { get; set; } = [];

    public IList<int> Horizons { get; set; } = [3, 6, 12, 24, 48, 72];

    public int ForecastDays { get; set; } = 4;

    public ParameterOptions Parameters { get; set; } = new();

    public string OpenMeteoBaseUrl { get; set; } = "https://api.open-meteo.com/";

    public MqttOptions Mqtt { get; set; } = new();

    public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromMinutes(20);

    public EnrichmentOptions Enrichment { get; set; } = new();

    public PersistenceOptions Persistence { get; set; } = new();

    public string PersistencePath { get; set; } = "data/njord-journal.db";
}
