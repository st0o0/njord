using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Health;

internal sealed class DataDirectoryHealthCheck(IOptions<PersistenceOptions> persistence) : IHealthCheck
{
    private const string ProbeFileName = ".njord-health-probe";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var directory = ResolveDirectory(persistence.Value);

        if (!Directory.Exists(directory))
        {
            return Task.FromResult(
                Evaluate(directory, isWritable: false, error: "directory does not exist"));
        }

        try
        {
            var probe = Path.Combine(directory, ProbeFileName);
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return Task.FromResult(Evaluate(directory, isWritable: true, error: null));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Evaluate(directory, isWritable: false, error: ex.Message));
        }
    }

    internal static HealthCheckResult Evaluate(string directoryPath, bool isWritable, string? error) =>
        isWritable
            ? HealthCheckResult.Healthy($"Data directory writable: {directoryPath}")
            : HealthCheckResult.Unhealthy($"Data directory not writable: {directoryPath} ({error})");

    internal static string ResolveDirectory(PersistenceOptions options) =>
        Path.GetDirectoryName(Path.GetFullPath(options.DataPath)) ?? string.Empty;
}
