using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Persistence;
using Njord.Messages.Config;
using Njord.Model;

namespace Njord.Tests.Configuration;

public sealed class ConfigSetupContainerSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-config-container-{Guid.NewGuid():N}");

    [Fact]
    public void Options_are_bound_with_their_validators()
    {
        var services = new ServiceCollection();

        new ConfigSetupContainer().SetupServices(services, HostConfiguration.Sample(_tempDir));

        Assert.Multiple(
            () => Assert.Contains(services, d => d.ServiceType == typeof(IValidateOptions<NjordOptions>)
                && d.ImplementationType == typeof(NjordOptionsValidator)),
            () => Assert.Contains(services, d => d.ServiceType == typeof(IValidateOptions<PersistenceOptions>)
                && d.ImplementationType == typeof(PersistenceOptionsValidator)),
            () => Assert.Contains(services, d => d.ServiceType == typeof(IValidateOptions<StorageOptions>)
                && d.ImplementationType == typeof(StorageOptionsValidator)),
            () => Assert.Contains(services, d => d.ServiceType == typeof(ConfigView)),
            () => Assert.Contains(services, d => d.ServiceType == typeof(ServiceInfo)));
    }

    [Fact]
    public void Invalid_options_fail_startup_validation()
    {
        var configuration = HostConfiguration.Sample(_tempDir, new Dictionary<string, string?> { ["Njord:ForecastDays"] = "40" });
        using var provider = HostConfiguration.Services(configuration).BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(ex.Failures, f => f.Contains("ForecastDays must be between 1 and 16."));
    }

    [Fact]
    public void The_resolved_parameter_set_comes_from_the_configured_parameter_options()
    {
        var configuration = HostConfiguration.Sample(_tempDir);
        using var provider = HostConfiguration.Services(configuration).BuildServiceProvider();

        var resolved = provider.GetRequiredService<ResolvedParameterSet>();
        var expected = provider.GetRequiredService<IOptions<NjordOptions>>().Value.Parameters.Resolve();

        Assert.Multiple(
            () => Assert.Equal(expected.Hourly, resolved.Hourly),
            () => Assert.Equal(expected.Daily, resolved.Daily));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
