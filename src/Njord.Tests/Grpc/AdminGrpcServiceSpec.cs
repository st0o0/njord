using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc;
using Njord.Grpc.V2;

namespace Njord.Tests.Grpc;

public sealed class AdminGrpcServiceSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-test-{Guid.NewGuid():N}");

    [Fact(Timeout = 5000)]
    public async Task GetConfig_returns_current_configuration()
    {
        var service = CreateService();

        var config = await service.GetConfig(new GetConfigRequest(), TestServerCallContext.Create());

        var location = Assert.Single(config.Locations);
        Assert.Equal("lucerne", location.Name);
        Assert.Equal(47.05, location.Latitude);
        Assert.Equal(8.31, location.Longitude);
        Assert.Equal(new[] { "icon_d2" }, config.DefaultModels);
        Assert.Equal(new[] { "icon_d2" }, location.Models);
        Assert.Equal(new[] { 3, 6, 12, 24, 48, 72 }, config.Horizons);
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

        var response = await service.SetLocations(request, TestServerCallContext.Create());

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

        var response = await service.SetLocations(new SetLocationsRequest(), TestServerCallContext.Create());

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
            TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Equal(1800, response.Config.PollIntervalSeconds);
        Assert.Equal(4, response.Config.ForecastDays);
        Assert.Equal(new[] { 3, 6, 12, 24, 48, 72 }, response.Config.Horizons);
        Assert.Equal(new[] { "icon_d2" }, response.Config.DefaultModels);
        Assert.Single(response.Config.Locations);
    }

    [Fact(Timeout = 5000)]
    public async Task SetSettings_rejects_poll_interval_below_minimum()
    {
        var service = CreateService();

        var response = await service.SetSettings(
            new SetSettingsRequest { PollIntervalSeconds = 30 },
            TestServerCallContext.Create());

        Assert.False(response.Applied);
        Assert.Equal("Poll interval must be at least 60 seconds", response.RejectionReason);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_sets_override()
    {
        var service = CreateService();

        var response = await service.SetBudget(
            new SetBudgetRequest { RequestsPerMonth = 500_000 },
            TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.NotNull(response.Config.BudgetOverride);
        Assert.Equal(500_000, response.Config.BudgetOverride.RequestsPerMonth);
        Assert.Equal(600, response.Config.BudgetOverride.RequestsPerMinute);
        Assert.Equal(500_000, response.BudgetProjection.MonthlyLimit);
    }

    [Fact(Timeout = 5000)]
    public async Task SetBudget_clears_override_when_empty()
    {
        var options = DefaultOptions();
        options.BudgetOverride = new RequestBudget(100_000, 60);
        var service = CreateService(options);

        var response = await service.SetBudget(new SetBudgetRequest(), TestServerCallContext.Create());

        Assert.True(response.Applied);
        Assert.Null(response.Config.BudgetOverride);
        Assert.Equal(RequestBudget.OpenMeteoFreeTier.RequestsPerMonth, response.BudgetProjection.MonthlyLimit);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private AdminGrpcService CreateService(NjordOptions? options = null)
    {
        var monitor = new MutableOptionsMonitor(options ?? DefaultOptions());
        var persistence = new ConfigPersistence(_tempDir);
        return new AdminGrpcService(monitor, persistence,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminGrpcService>.Instance);
    }

    private sealed class MutableOptionsMonitor(NjordOptions value) : IOptionsMonitor<NjordOptions>
    {
        public NjordOptions CurrentValue { get; set; } = value;
        public NjordOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NjordOptions, string?> listener) => null;
    }
}
