using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Health;

namespace Njord.Tests.Health;

public sealed class DataDirectoryHealthCheckSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-data-dir-{Guid.NewGuid():N}");

    [Fact]
    public void A_writable_directory_is_healthy()
    {
        var result = DataDirectoryHealthCheck.Evaluate("/data", _ => true);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Healthy, result.Status),
            () => Assert.Equal("Configuration directory /data is writable", result.Description));
    }

    [Fact]
    public void A_directory_that_cannot_be_written_is_degraded()
    {
        var result = DataDirectoryHealthCheck.Evaluate("/data", _ => false);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Degraded, result.Status),
            () => Assert.Equal(
                "Configuration directory /data is not writable; configuration changes cannot be saved",
                result.Description));
    }

    [Fact]
    public void The_probe_accepts_an_existing_directory_and_leaves_no_file_behind()
    {
        Directory.CreateDirectory(_tempDir);

        var writable = DirectoryProbe.CanWrite(_tempDir);

        Assert.Multiple(
            () => Assert.True(writable),
            () => Assert.Empty(Directory.GetFileSystemEntries(_tempDir)));
    }

    [Fact]
    public void The_probe_rejects_a_missing_directory()
    {
        Assert.False(DirectoryProbe.CanWrite(_tempDir));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
