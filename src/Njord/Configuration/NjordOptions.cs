namespace Njord.Configuration;

/// <summary>Root options bound from the <c>Njord</c> configuration section.</summary>
public sealed class NjordOptions
{
    public const string SectionName = "Njord";

    /// <summary>Replaces the free-tier default entirely when set (self-throttling below the soft limits).</summary>
    public RequestBudget? BudgetOverride { get; set; }

    public RequestBudget EffectiveBudget => BudgetOverride ?? RequestBudget.OpenMeteoFreeTier;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(60);

    public IList<LocationOptions> Locations { get; set; } = [];

    /// <summary>Open-Meteo model ids (free-form strings, e.g. "icon_d2").</summary>
    public IList<string> Models { get; set; } = [];

    /// <summary>Forecast horizons in hours; the HA entity grid derives from these × models × parameters.</summary>
    public IList<int> Horizons { get; set; } = [3, 6, 12, 24, 48, 72];

    /// <summary>Number of forecast days requested from Open-Meteo (determines daily day-offsets and hourly grid).</summary>
    public int ForecastDays { get; set; } = 4;

    /// <summary>Parameter group and variable selection.</summary>
    public ParameterOptions Parameters { get; set; } = new();

    public MqttOptions Mqtt { get; set; } = new();

    public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromMinutes(20);

    public EnrichmentOptions Enrichment { get; set; } = new();

    public PersistenceOptions Persistence { get; set; } = new();

    public string PersistencePath { get; set; } = "data/njord-journal.db";
}
