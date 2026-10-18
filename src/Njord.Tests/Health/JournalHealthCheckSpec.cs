using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration.Persistence;
using Njord.Health;
using Njord.Tests.Shared;

namespace Njord.Tests.Health;

public sealed class JournalHealthCheckSpec
{
    [Fact]
    public void A_writable_sqlite_directory_is_healthy()
    {
        var result = JournalHealthCheck.EvaluateSqlite("/data", _ => true);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Healthy, result.Status),
            () => Assert.Equal("SQLite journal directory /data is writable", result.Description));
    }

    [Fact]
    public void A_missing_sqlite_directory_is_unhealthy_and_names_the_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"njord-missing-{Guid.NewGuid():N}");

        var result = JournalHealthCheck.EvaluateSqlite(missing, DirectoryProbe.CanWrite);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal($"SQLite journal directory {missing} is missing or not writable", result.Description));
    }

    [Fact]
    public void The_sqlite_directory_comes_from_the_storage_path()
    {
        var directory = JournalHealthCheck.SqliteDirectory(
            AppSettingsDefaults.Options<PersistenceOptions>(), new StorageOptions { PersistencePath = Path.Combine("data", "njord.db") });

        Assert.Equal(Path.GetFullPath("data"), directory);
    }

    [Fact]
    public void The_sqlite_directory_comes_from_the_connection_string_when_one_is_configured()
    {
        var directory = JournalHealthCheck.SqliteDirectory(
            new PersistenceOptions { ConnectionString = $"Data Source={Path.Combine("other", "journal.db")}" },
            new StorageOptions { PersistencePath = Path.Combine("data", "njord.db") });

        Assert.Equal(Path.GetFullPath("other"), directory);
    }

    [Fact(Timeout = 15_000)]
    public async Task A_postgresql_journal_without_a_connection_string_is_unhealthy()
    {
        var result = await JournalHealthCheck.EvaluatePostgreSql(null, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Equal("PostgreSQL journal has no connection string", result.Description));
    }

    [Fact(Timeout = 15_000)]
    public async Task An_unreachable_postgresql_journal_is_unhealthy()
    {
        var result = await JournalHealthCheck.EvaluatePostgreSql(
            "Host=127.0.0.1;Port=1;Timeout=1;Pooling=false", TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.StartsWith("PostgreSQL journal is not reachable:", result.Description));
    }
}
