using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Mqtt.Configuration;

namespace Njord.Mqtt.Tests;

public sealed class MqttSetupContainerSpec
{
    private const string HealthCheckName = "mqtt-connection";

    private static IReadOnlyList<string> RegisteredHealthChecks(bool mqttEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Mqtt:Enabled"] = mqttEnabled.ToString(),
                ["Njord:Mqtt:Host"] = "localhost",
            })
            .Build();
        var services = new ServiceCollection();

        new MqttSetupContainer().SetupServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        return [.. provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name)];
    }

    [Fact]
    public void Mqtt_connection_health_check_is_registered_when_mqtt_is_enabled()
    {
        var names = RegisteredHealthChecks(mqttEnabled: true);

        Assert.Equal([HealthCheckName], names);
    }

    [Fact]
    public void Mqtt_connection_health_check_is_not_registered_when_mqtt_is_disabled()
    {
        var names = RegisteredHealthChecks(mqttEnabled: false);

        Assert.Empty(names);
    }
}
