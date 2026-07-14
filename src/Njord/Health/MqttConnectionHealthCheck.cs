using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Njord.Health;

internal sealed class MqttConnectionHealthCheck(NjordHealthState state, TimeProvider timeProvider) : IHealthCheck
{
    private static readonly TimeSpan UnhealthyThreshold = TimeSpan.FromMinutes(2);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (state.IsMqttConnected)
            return Task.FromResult(HealthCheckResult.Healthy("MQTT connected"));

        var disconnectedSince = state.MqttDisconnectedSince;
        if (disconnectedSince is null)
            return Task.FromResult(HealthCheckResult.Degraded("MQTT not yet connected"));

        var elapsed = timeProvider.GetUtcNow() - disconnectedSince.Value;

        return elapsed >= UnhealthyThreshold
            ? Task.FromResult(HealthCheckResult.Unhealthy($"MQTT disconnected for {elapsed.TotalSeconds:F0}s"))
            : Task.FromResult(HealthCheckResult.Degraded($"MQTT disconnected for {elapsed.TotalSeconds:F0}s"));
    }
}
