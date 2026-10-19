using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration.Health;

namespace Njord.Mqtt.Health;

internal sealed class MqttConnectionHealthCheck(
    AtomicStateHolder<MqttConnectionStatusResult> holder,
    TimeProvider timeProvider,
    IOptions<HealthOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = holder.Current;
        return Task.FromResult(status is null
            ? HealthCheckResult.Degraded("MQTT not yet connected")
            : Evaluate(status, timeProvider.GetUtcNow(), options.Value.MqttUnhealthyAfter));
    }

    internal static HealthCheckResult Evaluate(MqttConnectionStatusResult status, DateTimeOffset now, TimeSpan unhealthyAfter)
    {
        if (status.IsConnected)
        {
            return HealthCheckResult.Healthy("MQTT connected");
        }

        if (status.DisconnectedSince is not { } since)
        {
            return HealthCheckResult.Degraded("MQTT not yet connected");
        }

        var elapsed = now - since;

        return elapsed >= unhealthyAfter
            ? HealthCheckResult.Unhealthy($"MQTT disconnected for {elapsed.TotalSeconds:F0}s")
            : HealthCheckResult.Degraded($"MQTT disconnected for {elapsed.TotalSeconds:F0}s");
    }
}
