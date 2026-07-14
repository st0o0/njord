using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Configuration;

namespace Njord.Health;

internal sealed class PipelineHealthCheck(
    NjordHealthState state,
    TimeProvider timeProvider,
    IOptions<NjordOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var interval = options.Value.PollInterval;
        var lastPoll = state.LastSuccessfulPollUtc;

        if (lastPoll is null)
        {
            var uptime = now - state.ServiceStartedUtc;
            return uptime < interval * 2
                ? Task.FromResult(HealthCheckResult.Healthy("Waiting for first poll"))
                : Task.FromResult(HealthCheckResult.Unhealthy("No poll completed since startup"));
        }

        var elapsed = now - lastPoll.Value;

        if (elapsed < interval * 2)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"Last poll {elapsed.TotalMinutes:F0}m ago"));
        }

        if (elapsed < interval * 3)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Last poll {elapsed.TotalMinutes:F0}m ago (overdue)"));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy($"Last poll {elapsed.TotalMinutes:F0}m ago (stalled)"));
    }
}
