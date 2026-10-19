using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration.Health;
using Njord.Messages.Pipeline;
using Njord.Pipeline.Health;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests.Health;

public sealed class PipelineHealthCheckSpec
{
    private static readonly DateTimeOffset Started = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);
    private static readonly HealthOptions Options = AppSettingsDefaults.Options<HealthOptions>();

    private static PollHealthResult PolledMinutesAgo(int minutes, DateTimeOffset now) =>
        new(Started, now - TimeSpan.FromMinutes(minutes));

    [Fact]
    public void A_recent_poll_is_healthy()
    {
        var now = Started.AddHours(5);

        var result = PipelineHealthCheck.Evaluate(PolledMinutesAgo(45, now), Interval, now, Options);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Healthy, result.Status),
            () => Assert.Equal("Last poll 45m ago", result.Description));
    }

    [Fact]
    public void An_overdue_poll_is_degraded()
    {
        var now = Started.AddHours(5);

        var result = PipelineHealthCheck.Evaluate(PolledMinutesAgo(150, now), Interval, now, Options);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal("Last poll 150m ago (overdue)", result.Description));
    }

    [Fact]
    public void A_stalled_pipeline_is_unhealthy()
    {
        var now = Started.AddHours(5);

        var result = PipelineHealthCheck.Evaluate(PolledMinutesAgo(200, now), Interval, now, Options);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("Last poll 200m ago (stalled)", result.Description));
    }

    [Fact]
    public void The_configured_stalled_factor_moves_the_boundary()
    {
        var now = Started.AddHours(5);
        var lenient = AppSettingsDefaults.Options<HealthOptions>(o => o.PollStalledFactor = 4);

        var result = PipelineHealthCheck.Evaluate(PolledMinutesAgo(210, now), Interval, now, lenient);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public void Waiting_for_the_first_poll_is_healthy_within_the_grace_period()
    {
        var now = Started.AddMinutes(30);

        var result = PipelineHealthCheck.Evaluate(new PollHealthResult(Started, null), Interval, now, Options);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Healthy, result.Status),
            () => Assert.Equal("Waiting for first poll", result.Description));
    }

    [Fact]
    public void No_poll_after_the_grace_period_is_unhealthy()
    {
        var now = Started.AddHours(2);

        var result = PipelineHealthCheck.Evaluate(new PollHealthResult(Started, null), Interval, now, Options);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("No poll completed since startup", result.Description));
    }
}
