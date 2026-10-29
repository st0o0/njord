using System.Reflection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Registration;

public sealed class FeatureRegistrationTests
{
    private static string ConfigSectionOf(IFeatureRule rule)
    {
        var configType = rule.GetType().GetConstructors().Single().GetParameters()
            .Select(static p => p.ParameterType)
            .Single(static t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IOptions<>))
            .GetGenericArguments()[0];
        var method = typeof(IConfigSection).GetMethod(nameof(IConfigSection.SectionOf), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.MakeGenericMethod(configType).Invoke(null, null));
    }

    [Fact]
    public void Every_rule_is_named_after_its_config_section()
    {
        var rules = FeatureServices.AllFeatureRules();

        Assert.Equal(66, rules.Count);
        foreach (var rule in rules)
        {
            Assert.Equal($"Njord:Features:{rule.Descriptor.Category}:{rule.Descriptor.Name}", ConfigSectionOf(rule));
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
