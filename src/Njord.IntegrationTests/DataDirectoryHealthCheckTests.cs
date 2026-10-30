using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Health;

namespace Njord.IntegrationTests;

public sealed class DataDirectoryHealthCheckTests
{
    [Fact]
    public void Writable_directory_is_healthy()
    {
        var result = DataDirectoryHealthCheck.Evaluate("/data", isWritable: true, error: null);

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Healthy, result.Status),
            () => Assert.Contains("/data", result.Description));
    }

    [Fact]
    public void Missing_directory_is_unhealthy()
    {
        var result = DataDirectoryHealthCheck.Evaluate("/data", isWritable: false, error: "directory does not exist");

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Contains("/data", result.Description),
            () => Assert.Contains("directory does not exist", result.Description));
    }

    [Fact]
    public void Unwritable_directory_is_unhealthy()
    {
        var result = DataDirectoryHealthCheck.Evaluate("/data", isWritable: false, error: "Access denied");

        Assert.Multiple(
            () => Assert.Equal(HealthStatus.Unhealthy, result.Status),
            () => Assert.Contains("/data", result.Description),
            () => Assert.Contains("Access denied", result.Description));
    }
}
