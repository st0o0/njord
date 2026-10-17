namespace Njord.Architecture.Tests;

public sealed class MqttBoundarySpec
{
    private const string MqttAssembly = "Njord.Mqtt";

    private static readonly HashSet<string> CoreAllowList =
    [
        "Njord.Core.Configuration.Mqtt.MqttOptions",
        "Njord.Core.Configuration.Mqtt.MqttOptionsValidator",
        "Njord.Core.Actors.IMqttConnectionActor",
        "Njord.Core.Actors.IMqttStatePublisher",
        "Njord.Core.Health.MqttHealthSnapshot",
    ];

    [Fact]
    public void Mqtt_specific_types_are_declared_in_Njord_Mqtt_or_on_the_Core_allow_list()
    {
        var offenders = NjordArchitecture.ProductionAssemblies
            .Where(a => a.GetName().Name != MqttAssembly)
            .SelectMany(a => a.GetTypes())
            .Where(t => t.DeclaringType is null)
            .Where(t => !t.Name.StartsWith('<'))
            .Where(IsMqttSpecific)
            .Select(t => t.FullName ?? t.Name)
            .Where(n => !CoreAllowList.Contains(n))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Core_allow_list_entries_exist()
    {
        var core = NjordArchitecture.ProductionAssemblies.Single(a => a.GetName().Name == "Njord.Core");
        var declared = core.GetTypes().Select(t => t.FullName).ToHashSet();

        Assert.Empty(CoreAllowList.Where(n => !declared.Contains(n)).Order(StringComparer.Ordinal));
    }

    private static bool IsMqttSpecific(Type type)
        => type.Name.Contains("Mqtt", StringComparison.Ordinal)
           || (type.Namespace ?? string.Empty).Split('.').Any(s => s.Contains("Mqtt", StringComparison.Ordinal));
}
