using Microsoft.Extensions.DependencyInjection;
using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class CoreSetupContainerSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-core-container-{Guid.NewGuid():N}");

    [Fact]
    public void TimeProvider_is_registered_exactly_once_as_the_system_provider()
    {
        var services = HostConfiguration.Services(HostConfiguration.Sample(_tempDir));

        var registration = Assert.Single(services, d => d.ServiceType == typeof(TimeProvider));

        Assert.Same(TimeProvider.System, registration.ImplementationInstance);
    }

    [Fact]
    public void The_meter_allow_list_holds_the_documented_meter_names()
    {
        Assert.Equal(
            new[] { "Njord.Budget", "Njord.Core", "Njord.Mqtt", "Njord.OpenMeteo", "Njord.Pipeline", "Njord.SensorHub" },
            CoreSetupContainer.NjordMeters.Order());
    }

    [Fact]
    public void The_core_container_registers_neither_options_nor_health_services()
    {
        var services = new ServiceCollection();

        new CoreSetupContainer().SetupServices(services, HostConfiguration.Sample(_tempDir));

        Assert.Equal([typeof(TimeProvider)], services.Select(d => d.ServiceType));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
