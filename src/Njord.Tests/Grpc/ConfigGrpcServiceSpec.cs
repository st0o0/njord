using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc;
using Njord.Grpc.V1;

namespace Njord.Tests.Grpc;

public sealed class ConfigGrpcServiceSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}");

    protected override async Task AfterAllAsync()
    {
        await base.AfterAllAsync();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class MutableOptionsMonitor(NjordOptions value) : IOptionsMonitor<NjordOptions>
    {
        public NjordOptions CurrentValue { get; set; } = value;
        public NjordOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NjordOptions, string?> listener) => null;
    }

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2", "ecmwf_ifs025"],
        Horizons = [3, 6, 12, 24, 48, 72],
        ForecastDays = 4,
        PollInterval = TimeSpan.FromMinutes(60),
        Parameters = new ParameterOptions { Groups = ["Weather"] },
    };

    private ConfigGrpcService CreateService(NjordOptions? options = null, MutableOptionsMonitor? monitor = null)
    {
        options ??= DefaultOptions();
        monitor ??= new MutableOptionsMonitor(options);
        var persistence = new ConfigPersistence(_tempDir);
        var tracker = new BudgetTracker();
        var registry = ActorRegistry;
        return new ConfigGrpcService(monitor, persistence, tracker, registry);
    }

    // ═══════════════════════════════════════
    // MapConfig
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public void MapConfig_returns_locations_with_resolved_models()
    {
        var config = ConfigGrpcService.MapConfig(DefaultOptions());

        Assert.Single(config.Locations);
        Assert.Equal("lucerne", config.Locations[0].Name);
        Assert.Equal(47.05, config.Locations[0].Latitude);
        Assert.Contains("icon_d2", config.Locations[0].Models);
        Assert.Contains("ecmwf_ifs025", config.Locations[0].Models);
    }

    [Fact(Timeout = 5000)]
    public void MapConfig_returns_horizons_and_forecast_days()
    {
        var config = ConfigGrpcService.MapConfig(DefaultOptions());

        Assert.Equal([3, 6, 12, 24, 48, 72], config.Horizons);
        Assert.Equal(4, config.ForecastDays);
        Assert.Equal(3600, config.PollIntervalSeconds);
    }

    [Fact(Timeout = 5000)]
    public void MapConfig_returns_default_models()
    {
        var config = ConfigGrpcService.MapConfig(DefaultOptions());

        Assert.Contains("icon_d2", config.DefaultModels);
        Assert.Contains("ecmwf_ifs025", config.DefaultModels);
    }

    [Fact(Timeout = 5000)]
    public void MapConfig_returns_parameter_groups()
    {
        var config = ConfigGrpcService.MapConfig(DefaultOptions());

        Assert.Contains("Weather", config.Parameters.Groups);
    }

    [Fact(Timeout = 5000)]
    public void MapConfig_returns_detailed_enrichment_config()
    {
        var config = ConfigGrpcService.MapConfig(DefaultOptions());

        Assert.True(config.Enrichment.Consensus.Enabled);
        Assert.Equal("Median", config.Enrichment.Consensus.Method);
        Assert.True(config.Enrichment.Alerts.Enabled);
        Assert.True(config.Enrichment.Derived.Enabled);
        Assert.False(config.Enrichment.Trends.Enabled);
        Assert.False(config.Enrichment.Energy.Enabled);
        Assert.False(config.Enrichment.History.Enabled);
    }

    [Fact(Timeout = 5000)]
    public void MapConfig_returns_budget_projection()
    {
        var config = ConfigGrpcService.MapConfig(DefaultOptions());

        Assert.True(config.BudgetProjection.WithinBudget);
        Assert.True(config.BudgetProjection.MonthlyLimit > 0);
    }

    [Fact(Timeout = 5000)]
    public async Task GetConfig_returns_current_config()
    {
        var service = CreateService();

        var response = await service.GetConfig(
            new GetConfigRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.Single(response.Locations);
        Assert.Equal("lucerne", response.Locations[0].Name);
    }

    // ═══════════════════════════════════════
    // AddLocation
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public async Task AddLocation_succeeds_for_new_location()
    {
        var service = CreateService();
        var request = new AddLocationRequest
        {
            Name = "zurich",
            Latitude = 47.37,
            Longitude = 8.54,
        };

        var response = await service.AddLocation(request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Equal(2, response.Config.Locations.Count);
        Assert.Contains(response.Config.Locations, l => l.Name == "zurich");
    }

    [Fact(Timeout = 5000)]
    public async Task AddLocation_rejected_for_duplicate()
    {
        var service = CreateService();
        var request = new AddLocationRequest
        {
            Name = "lucerne",
            Latitude = 47.05,
            Longitude = 8.31,
        };

        var response = await service.AddLocation(request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.False(response.Applied);
        Assert.Contains("already exists", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task AddLocation_rejected_for_empty_name()
    {
        var service = CreateService();
        var request = new AddLocationRequest { Name = "", Latitude = 47.0, Longitude = 8.0 };

        var response = await service.AddLocation(request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.False(response.Applied);
        Assert.Contains("required", response.RejectionReason);
    }

    // ═══════════════════════════════════════
    // RemoveLocation
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public async Task RemoveLocation_succeeds_for_existing_location()
    {
        var options = DefaultOptions();
        options.Locations.Add(new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 });
        var service = CreateService(options);

        var response = await service.RemoveLocation(
            new RemoveLocationRequest { Name = "zurich" },
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Single(response.Config.Locations);
        Assert.DoesNotContain(response.Config.Locations, l => l.Name == "zurich");
    }

    [Fact(Timeout = 5000)]
    public async Task RemoveLocation_rejected_for_unknown()
    {
        var service = CreateService();

        var response = await service.RemoveLocation(
            new RemoveLocationRequest { Name = "unknown" },
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.False(response.Applied);
        Assert.Contains("not found", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task RemoveLocation_rejected_for_last_location_without_force()
    {
        var service = CreateService();

        var response = await service.RemoveLocation(
            new RemoveLocationRequest { Name = "lucerne", Force = false },
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.False(response.Applied);
        Assert.Contains("last location", response.RejectionReason);
    }

    // ═══════════════════════════════════════
    // UpdateForecastSettings
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public async Task UpdateForecastSettings_changes_poll_interval()
    {
        var service = CreateService();
        var request = new UpdateForecastSettingsRequest { PollIntervalSeconds = 1800 };

        var response = await service.UpdateForecastSettings(
            request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Equal(1800, response.Config.PollIntervalSeconds);
    }

    [Fact(Timeout = 5000)]
    public async Task UpdateForecastSettings_rejected_for_short_interval()
    {
        var service = CreateService();
        var request = new UpdateForecastSettingsRequest { PollIntervalSeconds = 10 };

        var response = await service.UpdateForecastSettings(
            request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.False(response.Applied);
        Assert.Contains("at least 60", response.RejectionReason);
    }

    // ═══════════════════════════════════════
    // UpdateEnrichmentConfig
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public async Task UpdateEnrichmentConfig_enables_energy()
    {
        var service = CreateService();
        var request = new UpdateEnrichmentConfigRequest
        {
            Energy = new EnergyConfig { Enabled = true },
        };

        var response = await service.UpdateEnrichmentConfig(
            request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.True(response.Config.Enrichment.Energy.Enabled);
    }

    [Fact(Timeout = 5000)]
    public async Task UpdateEnrichmentConfig_changes_consensus_method()
    {
        var service = CreateService();
        var request = new UpdateEnrichmentConfigRequest
        {
            Consensus = new ConsensusConfig { Method = "Mean" },
        };

        var response = await service.UpdateEnrichmentConfig(
            request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Equal("Mean", response.Config.Enrichment.Consensus.Method);
    }

    // ═══════════════════════════════════════
    // UpdateBudget
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public async Task UpdateBudget_sets_override()
    {
        var service = CreateService();
        var request = new UpdateBudgetRequest
        {
            RequestsPerMonth = 100_000,
            RequestsPerMinute = 100,
        };

        var response = await service.UpdateBudget(
            request, ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.NotNull(response.Config.BudgetOverride);
        Assert.Equal(100_000, response.Config.BudgetOverride.RequestsPerMonth);
        Assert.Equal(100, response.Config.BudgetOverride.RequestsPerMinute);
    }

    [Fact(Timeout = 5000)]
    public async Task UpdateBudget_clears_override_when_no_values()
    {
        var options = DefaultOptions();
        options.BudgetOverride = new RequestBudget(100_000, 100);
        var service = CreateService(options);

        var response = await service.UpdateBudget(
            new UpdateBudgetRequest(),
            ForecastGrpcServiceSpec.TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Null(response.Config.BudgetOverride);
    }

    // ═══════════════════════════════════════
    // CloneOptions
    // ═══════════════════════════════════════

    [Fact(Timeout = 5000)]
    public void CloneOptions_creates_independent_copy()
    {
        var original = DefaultOptions();

        var clone = ConfigGrpcService.CloneOptions(original);

        clone.Locations.Add(new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 });
        clone.Models.Add("gfs_seamless");

        Assert.Single(original.Locations);
        Assert.Equal(2, original.Models.Count);
    }
}
