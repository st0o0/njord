using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Mqtt.Transport;
using Njord.Ingest;
using Servus.Core.Application.Startup;

namespace Njord.Tests.Configuration;

public sealed class NjordServiceSetupSpec
{
    private static async Task DisposeProviderAsync(ServiceProvider provider)
    {
        await provider.DisposeAsync();
    }

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Locations:0:Name"] = "Lucerne",
                ["Njord:Locations:0:Latitude"] = "47.05",
                ["Njord:Locations:0:Longitude"] = "8.31",
                ["Njord:Models:0"] = "icon_d2",
                ["Njord:Mqtt:Host"] = "localhost",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var setup = new NjordServiceSetup();
        setup.SetupServices(services, config);

        return services.BuildServiceProvider();
    }

    [Fact(Timeout = 5000)]
    public void Implements_IServiceSetupContainer()
    {
        IServiceSetupContainer setup = new NjordServiceSetup();

        Assert.NotNull(setup);
    }

    [Fact(Timeout = 5000)]
    public async Task Registers_NjordOptions_with_validation()
    {
        var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<NjordOptions>>().Value;

        Assert.Single(options.Locations);
        Assert.Equal("Lucerne", options.Locations[0].Name);
        await DisposeProviderAsync(provider);
    }

    [Fact(Timeout = 5000)]
    public async Task Registers_ResolvedParameterSet()
    {
        var provider = BuildProvider();

        var parameters = provider.GetRequiredService<ResolvedParameterSet>();

        Assert.NotEmpty(parameters.Hourly);
        await DisposeProviderAsync(provider);
    }

    [Fact(Timeout = 5000)]
    public async Task Registers_TimeProvider()
    {
        var provider = BuildProvider();

        var timeProvider = provider.GetRequiredService<TimeProvider>();

        Assert.NotNull(timeProvider);
        await DisposeProviderAsync(provider);
    }

    [Fact(Timeout = 5000)]
    public async Task Registers_ingest_services()
    {
        var provider = BuildProvider();

        var client = provider.GetRequiredService<IOpenMeteoClient>();

        Assert.NotNull(client);
        await DisposeProviderAsync(provider);
    }

    [Fact(Timeout = 5000)]
    public async Task Registers_egress_services()
    {
        var provider = BuildProvider();

        var connection = provider.GetRequiredService<IMqttConnection>();
        var transport = provider.GetRequiredService<IMqttTransport>();

        Assert.NotNull(connection);
        Assert.NotNull(transport);
        await DisposeProviderAsync(provider);
    }
}
