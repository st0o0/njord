using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Budget.Configuration;
using Njord.Configuration;
using Njord.Grpc.Configuration;
using Njord.Mqtt.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Pipeline.Configuration;
using Njord.SensorHub.Configuration;
using Servus.Application.Startup;

namespace Njord.Tests.Configuration;

// The host's service registrations (every setup container that Program.cs applies before the actor system),
// built over the sample configuration plus per-test overrides.
internal static class HostConfiguration
{
    public static IServiceCollection Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();

        IServiceSetupContainer[] containers =
        [
            new CoreSetupContainer(),
            new ConfigSetupContainer(),
            new HealthSetupContainer(),
            new OpenMeteoSetupContainer(),
            new BudgetSetupContainer(),
            new SensorSetupContainer(),
            new PipelineSetupContainer(),
            new AnalysisSetupContainer(),
            new MqttSetupContainer(),
            new GrpcSetupContainer(),
        ];
        foreach (var container in containers)
        {
            container.SetupServices(services, configuration);
        }

        return services;
    }

    public static IConfigurationRoot Sample(string tempDir, IDictionary<string, string?>? overrides = null) =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Samples", "appsettings.Example.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:PersistencePath"] = Path.Combine(tempDir, "njord.db"),
            })
            .AddInMemoryCollection(overrides ?? new Dictionary<string, string?>())
            .Build();
}
