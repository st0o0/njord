using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Domain.Options;
using Njord.Grpc.V2;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class AdminGrpcServiceSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}");

    [Fact(Timeout = 5000)]
    public async Task GetConfig_returns_current_configuration()
    {
        var service = CreateService();

        var config = await service.GetConfig(new GetConfigRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var location = Assert.Single(config.Locations);
        Assert.Equal("lucerne", location.Name);
        Assert.Equal(47.05, location.Latitude);
        Assert.Equal(8.31, location.Longitude);
        Assert.Equal(new[] { "icon_d2" }, config.DefaultModels);
        Assert.Equal(new[] { "icon_d2" }, location.Models);
        Assert.Contains(3, config.Horizons);
        Assert.Contains(72, config.Horizons);
        Assert.Equal(4, config.ForecastDays);
        Assert.Equal(3600, config.PollIntervalSeconds);
    }

    [Fact(Timeout = 5000)]
    public async Task SetLocations_replaces_all_locations()
    {
        var service = CreateService();
        var request = new SetLocationsRequest
        {
            Locations =
            {
                new LocationInput { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
                new LocationInput { Name = "bern", Latitude = 46.95, Longitude = 7.44, Models = { "gfs_seamless" } },
            },
        };

        var response = await service.SetLocations(request, TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.Equal(2, response.Config.Locations.Count);
        Assert.DoesNotContain(response.Config.Locations, l => l.Name == "lucerne");
        Assert.Contains(response.Config.Locations, l => l.Name == "zurich");

        var bern = response.Config.Locations.First(l => l.Name == "bern");
        Assert.Equal(new[] { "icon_d2", "gfs_seamless" }, bern.Models);
    }

    [Fact(Timeout = 5000)]
    public async Task SetLocations_rejects_empty_list()
    {
        var service = CreateService();

        var response = await service.SetLocations(new SetLocationsRequest(), TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Equal("Cannot set empty location list", response.RejectionReason);
        Assert.Null(response.Config);
    }

    [Fact(Timeout = 5000)]
    public async Task SetSettings_applies_partial_update()
    {
        var service = CreateService();

        var response = await service.SetSettings(
            new SetSettingsRequest { PollIntervalSeconds = 1800 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.Equal(1800, response.Config.PollIntervalSeconds);
        Assert.Equal(4, response.Config.ForecastDays);
        Assert.Contains(3, response.Config.Horizons);
        Assert.Contains(72, response.Config.Horizons);
        Assert.Equal(new[] { "icon_d2" }, response.Config.DefaultModels);
        Assert.Single(response.Config.Locations);
    }

    [Fact(Timeout = 5000)]
    public async Task SetSettings_rejects_poll_interval_below_minimum()
    {
        var service = CreateService();

        var response = await service.SetSettings(
            new SetSettingsRequest { PollIntervalSeconds = 30 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Equal("Poll interval must be at least 60 seconds", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_sets_override()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 500_000 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.True(response.Applied);
        Assert.NotNull(response.Config.BudgetOverride);
        Assert.Equal(500_000, response.Config.BudgetOverride.RequestsPerMonth);
        Assert.Equal(600, response.Config.BudgetOverride.RequestsPerMinute);
        Assert.Equal(500_000, response.BudgetProjection.MonthlyLimit);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_clears_override_when_empty()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var setResponse = await service.SetBudget(new SetBudgetRequest { RequestsPerMonth = 100_000 }, ctx);
        Assert.True(setResponse.Applied);
        Assert.NotNull(setResponse.Config.BudgetOverride);

        var clearResponse = await service.SetBudget(new SetBudgetRequest(), ctx);
        Assert.True(clearResponse.Applied);

        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.True(config.BudgetProjection.WithinBudget);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_rejects_zero_monthly_budget()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 0 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_rejects_negative_monthly_budget()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = -100 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_rejects_zero_per_minute_budget()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMinute = 0 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_rejects_when_valid_monthly_but_zero_per_minute()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 300_000, RequestsPerMinute = 0 },
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.False(response.Applied);
        Assert.Contains("must be greater than zero", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task SetEnrichment_disable_alerts_is_reflected_in_GetConfig()
    {
        var service = CreateService();
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var response = await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { Enabled = false } }, ctx);

        Assert.True(response.Applied);
        Assert.False(response.Config.Enrichment.Alerts.Enabled);

        var config = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.False(config.Enrichment.Alerts.Enabled);
    }

    [Fact(Timeout = 5000)]
    public async Task SetEnrichment_overrides_baseline_config()
    {
        var service = CreateService(opt => opt.Enrichment.Alerts.Enabled = true);
        var ctx = TestServerCallContext.Create(TestContext.Current.CancellationToken);

        var configBefore = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.True(configBefore.Enrichment.Alerts.Enabled);

        await service.SetEnrichment(
            new SetEnrichmentRequest { Alerts = new AlertConfig { Enabled = false } }, ctx);

        var configAfter = await service.GetConfig(new GetConfigRequest(), ctx);
        Assert.False(configAfter.Enrichment.Alerts.Enabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private AdminGrpcService CreateService(Action<NjordOptions>? configureBaseline = null)
    {
        var baseline = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        configureBaseline?.Invoke(baseline);

        var overridePath = Path.Combine(_tempDir, "appsettings.Override.json");

        var configBuilder = new ConfigurationBuilder();
        var configEntries = new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = baseline.Locations[0].Name,
            ["Njord:Locations:0:Latitude"] = baseline.Locations[0].Latitude.ToString(),
            ["Njord:Locations:0:Longitude"] = baseline.Locations[0].Longitude.ToString(),
            ["Njord:Models:0"] = baseline.Models[0],
            ["Njord:Enrichment:Alerts:Enabled"] = baseline.Enrichment.Alerts.Enabled.ToString(),
            ["Njord:ForecastDays"] = baseline.ForecastDays.ToString(),
        };
        if (baseline.BudgetOverride is { } bo)
        {
            configEntries["Njord:BudgetOverride:RequestsPerMonth"] = bo.RequestsPerMonth.ToString();
            configEntries["Njord:BudgetOverride:RequestsPerMinute"] = bo.RequestsPerMinute.ToString();
        }

        configBuilder.AddInMemoryCollection(configEntries);

        configBuilder.AddJsonFile(overridePath, optional: true, reloadOnChange: false);
        var configRoot = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddOptions<NjordOptions>().Bind(configRoot.GetSection(NjordOptions.SectionName));
        var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<NjordOptions>>();

        var writable = new WritableNjordOptions(monitor, configRoot, overridePath);

        return new AdminGrpcService(monitor, writable);
    }
}
