using System.Reflection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Architecture.Tests;

// Configuration is fixed for the lifetime of the process, so options are read through IOptions<T> (or
// ConfigView). A monitor would only suggest a reload that never happens. Only constructor parameters are
// scanned: setup containers resolve options from the service provider to build start-time values and are
// outside the rule.
public sealed class OptionsAccessArchitectureSpec
{
    [Fact]
    public void No_production_constructor_takes_an_options_monitor()
    {
        var offenders = NjordArchitecture.ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.FullName!.Contains("Microsoft.CodeCoverage", StringComparison.Ordinal))
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Where(p => p.ParameterType.IsGenericType
                    && p.ParameterType.GetGenericTypeDefinition() == typeof(IOptionsMonitor<>))
                .Select(p => $"{t.FullName} injects {p.ParameterType.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Section_names_are_distinct_across_all_options_types()
    {
        var sections = NjordArchitecture.ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
            .Select(t => (Type: t.Name, Section: ConfigSections.PathOf(t)))
            .ToList();

        Assert.NotEmpty(sections);
        var duplicates = sections
            .GroupBy(s => s.Section, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(s => s.Type))}")
            .ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void The_polling_openmeteo_and_storage_options_bind_their_own_sections()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord:Polling", ConfigSections.PathOf<Core.Configuration.Polling.PollingOptions>()),
            () => Assert.Equal("Njord:OpenMeteo", ConfigSections.PathOf<Core.Configuration.OpenMeteo.OpenMeteoOptions>()),
            () => Assert.Equal("Njord:Storage", ConfigSections.PathOf<Core.Configuration.Persistence.StorageOptions>()));
    }
}
