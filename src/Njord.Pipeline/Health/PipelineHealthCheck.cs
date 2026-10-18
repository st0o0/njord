using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Health;

namespace Njord.Pipeline.Health;

internal sealed class PipelineHealthCheck(
    PollHealthState state,
    TimeProvider timeProvider,
    ConfigView config,
    IOptionsMonitor<HealthOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(Evaluate(state.Current, config.Polling.PollInterval, timeProvider.GetUtcNow(), options.CurrentValue));

    internal static HealthCheckResult Evaluate(
        PollHealth health, TimeSpan interval, DateTimeOffset now, HealthOptions options)
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
