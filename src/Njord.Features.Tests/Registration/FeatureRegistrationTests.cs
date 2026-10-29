using System.Reflection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Messages.Results;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Registration;

public sealed class FeatureRegistrationTests
{
    private static string ConfigSectionOf(IFeatureRule rule)
    {
        var configType = rule.GetType().GetConstructors().Single().GetParameters()
            .Select(static p => p.ParameterType)
            .Where(static t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IOptions<>))
            .Select(static t => t.GetGenericArguments()[0])
            .Single(static t => t.Name.EndsWith("Config", StringComparison.Ordinal));
        var method = typeof(IConfigSection).GetMethod(nameof(IConfigSection.SectionOf), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.MakeGenericMethod(configType).Invoke(null, null));
    }

    [Fact]
    public void Every_rule_is_named_after_its_config_section()
    {
        var rules = FeatureServices.AllFeatureRules();

        Assert.Equal(65, rules.Count);
        foreach (var rule in rules)
        {
            Assert.Equal($"Njord:Features:{rule.Descriptor.Name}", ConfigSectionOf(rule));
        }
    }

    [Fact]
    public void Feature_names_are_unique_per_category()
    {
        var duplicates = FeatureServices.AllFeatureRules()
            .GroupBy(static r => (r.Descriptor.Category, r.Descriptor.Name))
            .Where(static g => g.Count() > 1)
            .Select(static g => $"{g.Key.Category}:{g.Key.Name}")
            .ToList();

        Assert.Empty(duplicates);
    }
}
