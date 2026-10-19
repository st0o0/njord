using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration.Health;

namespace Njord.Mqtt.Health;

internal sealed class MqttConnectionHealthCheck(
    ActorRegistry actorRegistry,
    TimeProvider timeProvider,
    IOptions<HealthOptions> options) : IHealthCheck
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!actorRegistry.TryGet<IMqttConnectionActor>(out var mqttConnection))
        {
            return HealthCheckResult.Unhealthy("MQTT connection actor not available");
        }

        try
        {
            var result = await mqttConnection.Ask<MqttConnectionStatusResult>(
                new QueryMqttConnectionStatus(), AskTimeout, cancellationToken);
            return Evaluate(result, timeProvider.GetUtcNow(), options.Value.MqttUnhealthyAfter);
        }
        catch (AskTimeoutException)
        {
            return HealthCheckResult.Unhealthy("MQTT connection actor unresponsive");
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
