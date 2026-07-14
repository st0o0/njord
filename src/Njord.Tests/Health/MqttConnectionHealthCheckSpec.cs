using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Njord.Health;

namespace Njord.Tests.Health;

public sealed class MqttConnectionHealthCheckSpec
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero));

    private readonly NjordHealthState _state;
    private readonly MqttConnectionHealthCheck _check;

    public MqttConnectionHealthCheckSpec()
    {
        _state = new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() };
        _check = new MqttConnectionHealthCheck(_state, _time);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_healthy_when_connected()
    {
        _state.SetMqttConnected(_time.GetUtcNow());

        var result = await _check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_degraded_when_disconnected_less_than_2_minutes()
    {
        _state.SetMqttConnected(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromSeconds(10));
        _state.SetMqttDisconnected(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromSeconds(90));

        var result = await _check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_unhealthy_when_disconnected_2_minutes_or_more()
    {
        _state.SetMqttConnected(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromSeconds(10));
        _state.SetMqttDisconnected(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromMinutes(3));

        var result = await _check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_degraded_when_never_connected()
    {
        var result = await _check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
