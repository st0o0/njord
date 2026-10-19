using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Health;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline.Health;

internal sealed class PipelineHealthCheck(
    ActorRegistry actorRegistry,
    TimeProvider timeProvider,
    ConfigView config,
    IOptions<HealthOptions> options) : IHealthCheck
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!actorRegistry.TryGet<IPollManager>(out var pollManager))
        {
            return HealthCheckResult.Unhealthy("Poll manager not available");
        }

        try
        {
            var result = await pollManager.Ask<PollHealthResult>(
                new QueryPollHealth(), AskTimeout, cancellationToken);
            return Evaluate(result, config.Polling.PollInterval, timeProvider.GetUtcNow(), options.Value);
        }
        catch (AskTimeoutException)
        {
            return HealthCheckResult.Unhealthy("Poll manager unresponsive");
        }
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
