using System.Reflection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Architecture.Tests;

// Hot options (ConfigReloadPolicy) change while the service runs, so a consumer reads them through
// IOptionsMonitor<T> (or ConfigView) at the moment it needs them; a restart-required option never changes
// within a process, so IOptions<T> is the honest type and a monitor would only suggest a reload that never
// happens. Only constructor parameters are scanned: setup containers resolve options from the service
// provider to build start-time values and are outside the rule.
public sealed class OptionsAccessArchitectureSpec
{
    // Hot at the type level, but the consumer reads only the restart-required Enabled property, which is
    // fixed for the lifetime of the process.
    private static readonly IReadOnlyDictionary<(string Consumer, string Options), string> HotOptionsThroughIOptions =
        new Dictionary<(string, string), string>
        {
            [("Njord.Alerts.AlertAnalyzer", "AlertOptions")] = "reads only Enabled (restart-required)",
            [("Njord.Derived.DerivedAnalyzer", "DerivedOptions")] = "reads only Enabled (restart-required)",
            [("Njord.Trends.TrendAnalyzer", "TrendOptions")] = "reads only Enabled (restart-required)",
            [("Njord.Indices.IndexAnalyzer", "IndexOptions")] = "reads only Enabled (restart-required)",
            [("Njord.Pipeline.ForecastPipeline", "ConsensusOptions")] = "reads only Enabled (restart-required)",
        };

    // ConfigView and ConfigManager hold monitors of every section they report on, restart-required ones
    // included, to compare the persisted configuration with the running one.
    private static readonly IReadOnlyDictionary<(string Consumer, string Options), string> RestartRequiredOptionsThroughMonitor =
        new Dictionary<(string, string), string>
        {
            [("Njord.Core.Configuration.ConfigView", "NjordOptions")] = "reports the persisted forecast scope",
            [("Njord.Configuration.ConfigManager", "NjordOptions")] = "detects a changed forecast scope (restart required)",
        };

    private sealed record Injection(string Consumer, Type Options, bool Monitor);

    private static IEnumerable<Injection> Injections() =>
        NjordArchitecture.ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.FullName!.Contains("Microsoft.CodeCoverage", StringComparison.Ordinal))
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType)
                .Where(IsOptionsHolder)
                .Select(p => new Injection(
                    t.FullName!,
                    p.GetGenericArguments()[0],
                    p.GetGenericTypeDefinition() == typeof(IOptionsMonitor<>))))
            .Distinct();

    private static bool IsOptionsHolder(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(IOptions<>)
            || type.GetGenericTypeDefinition() == typeof(IOptionsMonitor<>));

    [Fact]
    public void Every_injected_options_type_is_classified_in_the_reload_policy()
    {
        var unclassified = Injections()
            .Where(i => !ConfigReloadPolicy.IsClassified(i.Options))
            .Select(i => $"{i.Consumer} injects {i.Options.Name}")
            .ToList();

        Assert.Empty(unclassified);
    }

    [Fact]
    public void Hot_options_are_not_injected_as_IOptions_outside_the_allow_list()
    {
        var offenders = Injections()
            .Where(i => !i.Monitor && ConfigReloadPolicy.IsClassified(i.Options)
                && ConfigReloadPolicy.For(i.Options) == ReloadMode.Hot)
            .Where(i => !HotOptionsThroughIOptions.ContainsKey((i.Consumer, i.Options.Name)))
            .Select(i => $"{i.Consumer} injects IOptions<{i.Options.Name}>")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Restart_required_options_are_not_injected_as_a_monitor_outside_the_allow_list()
    {
        var offenders = Injections()
            .Where(i => i.Monitor && ConfigReloadPolicy.IsClassified(i.Options)
                && ConfigReloadPolicy.For(i.Options) == ReloadMode.RestartRequired)
            .Where(i => !RestartRequiredOptionsThroughMonitor.ContainsKey((i.Consumer, i.Options.Name)))
            .Select(i => $"{i.Consumer} injects IOptionsMonitor<{i.Options.Name}>")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_allow_list_entry_is_still_needed()
    {
        var injections = Injections().ToList();

        var staleHot = HotOptionsThroughIOptions.Keys
            .Where(entry => !injections.Any(i =>
                !i.Monitor && i.Consumer == entry.Consumer && i.Options.Name == entry.Options))
            .Select(entry => $"IOptions<{entry.Options}> in {entry.Consumer}");
        var staleRestart = RestartRequiredOptionsThroughMonitor.Keys
            .Where(entry => !injections.Any(i =>
                i.Monitor && i.Consumer == entry.Consumer && i.Options.Name == entry.Options))
            .Select(entry => $"IOptionsMonitor<{entry.Options}> in {entry.Consumer}");

        Assert.Empty(staleHot.Concat(staleRestart));
    }

    [Fact]
    public void Allow_listed_hot_options_declare_Enabled_as_restart_required()
    {
        // The allow-list reason is a claim about the consumer; the policy at least has to back it.
        var hotAllowed = HotOptionsThroughIOptions.Keys
            .Select(entry => Injections().First(i => i.Consumer == entry.Consumer && i.Options.Name == entry.Options).Options)
            .Distinct();

        Assert.All(hotAllowed, options =>
            Assert.Equal(ReloadMode.RestartRequired, ConfigReloadPolicy.For(options, "Enabled")));
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
