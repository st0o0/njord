using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Health;
using Njord.Mqtt.Health;

namespace Njord.Tests.Configuration;

public sealed class HealthSetupContainerSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-health-container-{Guid.NewGuid():N}");

    private static HealthCheckRegistration[] Registrations(ServiceProvider provider) =>
        [.. provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations];

    [Fact]
    public void The_checks_of_the_core_features_are_registered_with_their_failure_status()
    {
        using var provider = HostConfiguration.Services(HostConfiguration.Sample(_tempDir, new Dictionary<string, string?>
        {
            ["Njord:Mqtt:Enabled"] = "false",
        })).BuildServiceProvider();

        var registrations = Registrations(provider).ToDictionary(r => r.Name, r => r.FailureStatus);

        Assert.Equal(
            new Dictionary<string, HealthStatus>
            {
                ["pipeline"] = HealthStatus.Unhealthy,
                ["budget"] = HealthStatus.Unhealthy,
                ["journal"] = HealthStatus.Unhealthy,
                ["data-directory"] = HealthStatus.Degraded,
            }.OrderBy(p => p.Key),
            registrations.OrderBy(p => p.Key));
    }

    [Fact]
    public void The_mqtt_check_is_registered_only_when_mqtt_is_enabled()
    {
        using var provider = HostConfiguration.Services(HostConfiguration.Sample(_tempDir, new Dictionary<string, string?>
        {
            ["Njord:Mqtt:Enabled"] = "true",
            ["Njord:Mqtt:Host"] = "broker",
        })).BuildServiceProvider();

        var registration = Assert.Single(Registrations(provider), r => r.Name == "mqtt-connection");

        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
    }

    [Fact]
    public void The_mqtt_session_counter_is_registered_when_mqtt_is_enabled()
    {
        using var provider = HostConfiguration.Services(HostConfiguration.Sample(_tempDir, new Dictionary<string, string?>
        {
            ["Njord:Mqtt:Enabled"] = "true",
            ["Njord:Mqtt:Host"] = "broker",
        })).BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<MqttSessionCounter>(), provider.GetRequiredService<MqttSessionCounter>());
    }

    [Fact]
    public void The_health_thresholds_bind_from_the_shipped_defaults()
    {
        using var provider = HostConfiguration.Services(HostConfiguration.Sample(_tempDir)).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<HealthOptions>>().Value;

        Assert.Multiple(
            () => Assert.Equal(2.0, options.PollOverdueFactor),
            () => Assert.Equal(3.0, options.PollStalledFactor),
            () => Assert.Equal(TimeSpan.FromMinutes(2), options.MqttUnhealthyAfter),
            () => Assert.Equal(5.0, options.BudgetDegradedBelowPercent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
