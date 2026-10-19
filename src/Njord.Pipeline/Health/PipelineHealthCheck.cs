using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Health;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline.Health;

internal sealed class PipelineHealthCheck(
    AtomicStateHolder<PollHealthResult> holder,
    TimeProvider timeProvider,
    ConfigView config,
    IOptions<HealthOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var health = holder.Current;
        return Task.FromResult(health is null
            ? HealthCheckResult.Degraded("No poll data yet")
            : Evaluate(health, config.Polling.PollInterval, timeProvider.GetUtcNow(), options.Value));
    }

    internal static HealthCheckResult Evaluate(
        PollHealthResult health, TimeSpan interval, DateTimeOffset now, HealthOptions options)
    {
        var overdue = interval * options.PollOverdueFactor;
        var stalled = interval * options.PollStalledFactor;

        if (health.LastSuccessfulPollUtc is not { } lastPoll)
        {
            return now - health.StartedUtc < overdue
                ? HealthCheckResult.Healthy("Waiting for first poll")
                : HealthCheckResult.Unhealthy("No poll completed since startup");
        }

        var elapsed = now - lastPoll;

        if (elapsed < overdue)
        {
            return HealthCheckResult.Healthy($"Last poll {elapsed.TotalMinutes:F0}m ago");
        }

        return elapsed < stalled
            ? HealthCheckResult.Degraded($"Last poll {elapsed.TotalMinutes:F0}m ago (overdue)")
            : HealthCheckResult.Unhealthy($"Last poll {elapsed.TotalMinutes:F0}m ago (stalled)");
    }
}
