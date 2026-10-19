using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Mqtt.Health;

namespace Njord.Mqtt.Tests.Health;

public sealed class MqttConnectionHealthCheckSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan UnhealthyAfter = TimeSpan.FromMinutes(2);

    [Fact]
    public void A_connected_broker_is_healthy()
    {
        var result = MqttConnectionHealthCheck.Evaluate(new MqttConnectionStatusResult(true, null), Now, UnhealthyAfter);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Healthy, result.Status),
            () => Assert.Equal("MQTT connected", result.Description));
    }

    [Fact]
    public void A_connection_that_was_never_established_is_degraded()
    {
        var result = MqttConnectionHealthCheck.Evaluate(new MqttConnectionStatusResult(false, null), Now, UnhealthyAfter);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("MQTT not yet connected", result.Description));
    }

    [Fact]
    public void A_recent_disconnect_is_degraded()
    {
        var result = MqttConnectionHealthCheck.Evaluate(
            new MqttConnectionStatusResult(false, Now - TimeSpan.FromSeconds(90)), Now, UnhealthyAfter);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("MQTT disconnected for 90s", result.Description));
    }

    [Fact]
    public void A_disconnect_at_the_threshold_is_unhealthy()
    {
        var result = MqttConnectionHealthCheck.Evaluate(
            new MqttConnectionStatusResult(false, Now - UnhealthyAfter), Now, UnhealthyAfter);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("MQTT disconnected for 120s", result.Description));
    }

    [Fact]
    public void The_configured_threshold_moves_the_boundary()
    {
        var status = new MqttConnectionStatusResult(false, Now - TimeSpan.FromMinutes(3));

        var strict = MqttConnectionHealthCheck.Evaluate(status, Now, TimeSpan.FromMinutes(2));
        var lenient = MqttConnectionHealthCheck.Evaluate(status, Now, TimeSpan.FromMinutes(5));

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, strict.Status),
            () => Assert.Equal(HealthStatus.Degraded, lenient.Status));
    }
}
