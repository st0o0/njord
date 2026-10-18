using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;

namespace Njord.Tests.Configuration;

public sealed class HealthSetupContainerSpec : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-health-container-{Guid.NewGuid():N}");

    [Fact]
    public void The_monitor_starts_at_the_service_start_time_with_the_configured_budget()
    {
        var services = HostConfiguration.Services(HostConfiguration.Sample(_tempDir, new Dictionary<string, string?>
        {
            ["Njord:Polling:BudgetOverride:RequestsPerMonth"] = "120000",
            ["Njord:Polling:BudgetOverride:RequestsPerMinute"] = "60",
        }));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<NjordHealthMonitor>();

        Assert.Multiple(
            () => Assert.Equal(Now, monitor.ServiceStartedUtc),
            () => Assert.Equal(
                new BudgetHealthSnapshot(0, 0, RequestBudget.OpenMeteoFreeTierDailyLimit, 120_000),
                monitor.Budget));
    }

    [Fact]
    public void The_pipeline_health_check_is_registered_and_the_monitor_is_a_singleton()
    {
        using var provider = HostConfiguration.Services(HostConfiguration.Sample(_tempDir)).BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Assert.Multiple(
            () => Assert.Contains(registrations, r => r.Name == "pipeline"),
            () => Assert.Same(
                provider.GetRequiredService<NjordHealthMonitor>(),
                provider.GetRequiredService<NjordHealthMonitor>()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
