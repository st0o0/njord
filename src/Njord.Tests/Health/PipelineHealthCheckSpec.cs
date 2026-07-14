using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Health;

namespace Njord.Tests.Health;

public sealed class PipelineHealthCheckSpec
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly NjordHealthState _state;
    private readonly NjordOptions _options = new() { PollInterval = TimeSpan.FromMinutes(60) };

    public PipelineHealthCheckSpec()
    {
        _state = new NjordHealthState { ServiceStartedUtc = _time.GetUtcNow() };
    }

    private PipelineHealthCheck CreateCheck() =>
        new(_state, _time, Options.Create(_options));

    [Fact(Timeout = 5000)]
    public async Task Returns_healthy_when_recent_poll()
    {
        _state.SetLastSuccessfulPoll(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromMinutes(45));

        var result = await CreateCheck().CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_degraded_when_overdue()
    {
        _state.SetLastSuccessfulPoll(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromMinutes(150));

        var result = await CreateCheck().CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_unhealthy_when_stalled()
    {
        _state.SetLastSuccessfulPoll(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromMinutes(200));

        var result = await CreateCheck().CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_healthy_during_startup_grace_period()
    {
        _time.Advance(TimeSpan.FromMinutes(30));

        var result = await CreateCheck().CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task Returns_unhealthy_after_startup_grace_period_with_no_polls()
    {
        _time.Advance(TimeSpan.FromMinutes(130));

        var result = await CreateCheck().CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
