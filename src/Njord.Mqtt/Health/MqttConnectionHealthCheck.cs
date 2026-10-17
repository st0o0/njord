using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Health;

namespace Njord.Mqtt.Health;

internal sealed class MqttConnectionHealthCheck(NjordHealthMonitor monitor, TimeProvider timeProvider) : IHealthCheck
{
    private static readonly TimeSpan UnhealthyThreshold = TimeSpan.FromMinutes(2);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var mqtt = monitor.Mqtt;
        if (mqtt.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy("MQTT connected"));
        }

        var disconnectedSince = mqtt.DisconnectedSince;
        if (disconnectedSince is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded("MQTT not yet connected"));
        }

        var elapsed = timeProvider.GetUtcNow() - disconnectedSince.Value;

        return elapsed >= UnhealthyThreshold
            ? Task.FromResult(HealthCheckResult.Unhealthy($"MQTT disconnected for {elapsed.TotalSeconds:F0}s"))
            : Task.FromResult(HealthCheckResult.Degraded($"MQTT disconnected for {elapsed.TotalSeconds:F0}s"));
    }
}
