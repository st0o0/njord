using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Persistence;
using Npgsql;

namespace Njord.Health;

internal sealed class JournalHealthCheck(IOptions<PersistenceOptions> persistence) : IHealthCheck
{
    internal static readonly TimeSpan PostgreSqlTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = persistence.Value;

        if (options.Provider == PersistenceProvider.PostgreSql)
        {
            return await EvaluatePostgreSql(options.ConnectionString, cancellationToken);
        }

        return EvaluateSqlite(SqliteDirectory(options), DirectoryProbe.CanWrite);
    }

    internal static string SqliteDirectory(PersistenceOptions options)
    {
        var path = options.ConnectionString is { Length: > 0 } connectionString
            ? new SqliteConnectionStringBuilder(connectionString).DataSource
            : options.DataPath;

        return Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
    }

    internal static HealthCheckResult EvaluateSqlite(string directory, Func<string, bool> canWrite) =>
        canWrite(directory)
            ? HealthCheckResult.Healthy($"SQLite journal directory {directory} is writable")
            : HealthCheckResult.Unhealthy($"SQLite journal directory {directory} is missing or not writable");

    internal static async Task<HealthCheckResult> EvaluatePostgreSql(string? connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("PostgreSQL journal has no connection string");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PostgreSqlTimeout);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(timeout.Token);
            var host = new NpgsqlConnectionStringBuilder(connectionString).Host;
            return HealthCheckResult.Healthy($"PostgreSQL journal reachable at {host}");
        }
        catch (Exception ex) when (ex is NpgsqlException or OperationCanceledException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy($"PostgreSQL journal is not reachable: {ex.Message}");
        }
    }
}
