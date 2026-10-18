using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration;

namespace Njord.Health;

internal sealed class DataDirectoryHealthCheck(ConfigOverrideFile overrideFile) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(Evaluate(Path.GetDirectoryName(overrideFile.Path) ?? string.Empty, DirectoryProbe.CanWrite));

    internal static HealthCheckResult Evaluate(string directory, Func<string, bool> canWrite) =>
        canWrite(directory)
            ? HealthCheckResult.Healthy($"Configuration directory {directory} is writable")
            : HealthCheckResult.Degraded($"Configuration directory {directory} is not writable; configuration changes cannot be saved");
}
