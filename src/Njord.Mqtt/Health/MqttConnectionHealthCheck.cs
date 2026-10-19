using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration.Health;

namespace Njord.Mqtt.Health;

internal sealed class MqttConnectionHealthCheck(
    ActorRegistry registry,
    TimeProvider timeProvider,
    IOptions<HealthOptions> options) : IHealthCheck
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!registry.TryGet<IMqttConnectionActor>(out var connection))
        {
            return HealthCheckResult.Degraded("MQTT connection not available");
        }

        try
        {
            var status = await connection.Ask<MqttConnectionStatusResult>(
                new QueryMqttConnectionStatus(), AskTimeout, cancellationToken);
            return Evaluate(status, timeProvider.GetUtcNow(), options.Value.MqttUnhealthyAfter);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("MQTT connection not responding");
        }
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
