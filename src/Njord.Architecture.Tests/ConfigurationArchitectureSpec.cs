using System.Reflection;
using Akka.Actor;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Core.Configuration;

namespace Njord.Architecture.Tests;

public sealed class ConfigurationArchitectureSpec
{
    // Change notifications have one subscriber (ConfigManager). The other entries read CurrentValue per call
    // and never subscribe: the gateway's budget provider (design D5), the admin/ops/health view of the
    // persisted configuration, and the section writer that clones the current value before an update.
    private static readonly string[] OptionsMonitorUsers =
    [
        "Njord.Configuration.ConfigManager",
        "Njord.OpenMeteo.OptionsBudgetProvider",
        "Njord.Core.Configuration.ConfigView",
        "Njord.Configuration.AnalysisConfigView",
        "Njord.Configuration.ConfigWriter",
    ];

    private static IEnumerable<Type> ProductionTypes() =>
        NjordArchitecture.ProductionAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.FullName!.Contains("Microsoft.CodeCoverage", StringComparison.Ordinal));

    private static bool IsOptionsMonitor(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptionsMonitor<>);

    private static bool InjectsOptionsMonitor(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Any(p => IsOptionsMonitor(p.ParameterType))
        || type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Any(f => IsOptionsMonitor(f.FieldType));

    // Compiler-generated state machines and closures count as their declaring type.
    private static Type OutermostType(Type type)
    {
        while (type.DeclaringType is { } declaring)
        {
            type = declaring;
        }

        return type;
    }

    [Fact]
    public void Actors_other_than_the_config_manager_do_not_inject_an_options_monitor()
    {
        var offenders = ProductionTypes()
            .Where(t => typeof(ActorBase).IsAssignableFrom(t) && t.FullName != "Njord.Configuration.ConfigManager")
            .Where(InjectsOptionsMonitor)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Only_the_listed_types_inject_an_options_monitor()
    {
        var users = ProductionTypes()
            .Where(InjectsOptionsMonitor)
            .Select(t => OutermostType(t).FullName!)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(OptionsMonitorUsers.Order(StringComparer.Ordinal), users);
    }

    [Fact]
    public void Every_options_type_has_a_reload_classification()
    {
        var unclassified = ProductionTypes()
            .Where(t => t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
            .Where(t => !ConfigReloadPolicy.IsClassified(t))
            .Select(t => t.FullName);

        Assert.Empty(unclassified);
    }

    [Theory]
    [InlineData("AlertOptions")]
    [InlineData("DerivedOptions")]
    [InlineData("TrendOptions")]
    [InlineData("IndexOptions")]
    [InlineData("ConsensusOptions")]
    public void Core_declares_no_analysis_feature_options(string name)
    {
        var core = typeof(NjordOptions).Assembly;

        Assert.DoesNotContain(core.GetTypes(), t => t.Name == name);
    }

    [Fact]
    public void Consensus_contains_no_options_types_and_does_not_reference_the_options_package()
    {
        var consensus = typeof(Njord.Consensus.LocationConsensus).Assembly;

        Assert.DoesNotContain(consensus.GetTypes(), t => t.Name.EndsWith("Options", StringComparison.Ordinal));
        Assert.DoesNotContain(consensus.GetTypes(), t => t.Namespace == "Njord.Consensus.Configuration");
        Assert.DoesNotContain(consensus.GetReferencedAssemblies(), a => a.Name == "Microsoft.Extensions.Options");
    }

    [Fact]
    public void Every_section_options_type_has_exactly_one_validator_next_to_it()
    {
        var core = typeof(NjordOptions).Assembly.GetTypes();
        var sectionTypes = core
            .Where(t => t.IsClass && t.Name.EndsWith("Options", StringComparison.Ordinal))
            .Where(t => t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
            .ToList();

        Assert.NotEmpty(sectionTypes);
        foreach (var type in sectionTypes)
        {
            var validators = core
                .Where(v => v.GetInterfaces().Contains(typeof(IValidateOptions<>).MakeGenericType(type)))
                .ToList();
            var validator = Assert.Single(validators);
            Assert.Equal(type.Namespace, validator.Namespace);
            Assert.Equal($"{type.Name}Validator", validator.Name);
        }
    }

    [Fact]
    public void Section_options_types_do_not_nest_other_section_options_types()
    {
        var sectionTypes = typeof(NjordOptions).Assembly.GetTypes()
            .Where(t => t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
            .ToHashSet();

        var nested = sectionTypes
            .SelectMany(t => t.GetProperties().Where(p => sectionTypes.Contains(p.PropertyType)).Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.Empty(nested);
    }
}
