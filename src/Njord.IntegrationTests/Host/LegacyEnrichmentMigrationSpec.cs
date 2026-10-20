using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.IntegrationTests.Host;

public sealed class LegacyEnrichmentMigrationSpec
{
    private static IConfiguration Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(entry => entry.Key, entry => (string?)entry.Value))
            .Build();

    private static FeatureOptions Bind(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLegacyEnrichmentMigration(configuration);
        services.AddOptions<FeatureOptions>().Bind(configuration.GetSection(FeatureOptions.SectionName));
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<FeatureOptions>>().Value;
    }

    [Fact]
    public void Should_expand_weather_alerts_to_every_weather_alert_feature()
    {
        var expanded = LegacyEnrichmentMigration.Expand(Config(("Njord:Enrichments:WeatherAlerts", "false")), out var keys);

        Assert.Multiple(
            () => Assert.Equal(["WeatherAlerts"], keys),
            () => Assert.Equal(14, expanded.Count),
            () => Assert.All(LegacyEnrichmentMigration.WeatherAlertKinds, kind => Assert.False(expanded[kind])));
    }

    [Fact]
    public void Should_map_other_legacy_keys_to_the_feature_of_the_same_name()
    {
        var expanded = LegacyEnrichmentMigration.Expand(
            Config(("Njord:Enrichments:FloodRisk", "true"), ("Njord:Enrichments:CommuteSafety", "false")), out var keys);

        Assert.Multiple(
            () => Assert.Equivalent(new[] { "FloodRisk", "CommuteSafety" }, keys),
            () => Assert.True(expanded[FeatureKind.FloodRisk]),
            () => Assert.False(expanded[FeatureKind.CommuteSafety]));
    }

    [Fact]
    public void Should_ignore_unknown_and_non_boolean_legacy_entries()
    {
        var expanded = LegacyEnrichmentMigration.Expand(
            Config(("Njord:Enrichments:Lava", "true"), ("Njord:Enrichments:FloodRisk", "maybe")), out var keys);

        Assert.Multiple(() => Assert.Empty(expanded), () => Assert.Empty(keys));
    }

    [Fact]
    public void Should_bind_the_migrated_legacy_toggles_into_the_feature_options()
    {
        var options = Bind(Config(("Njord:Enrichments:WeatherAlerts", "false"), ("Njord:Enrichments:FloodRisk", "true")));

        Assert.Multiple(
            () => Assert.False(options[FeatureKind.FrostAlert]),
            () => Assert.False(options[FeatureKind.HumidityAlert]),
            () => Assert.True(options[FeatureKind.FloodRisk]));
    }

    [Fact]
    public void Should_let_an_explicit_feature_entry_win_over_a_legacy_entry()
    {
        var options = Bind(Config(("Njord:Enrichments:WeatherAlerts", "false"), ("Njord:Features:FrostAlert", "true")));

        Assert.Multiple(
            () => Assert.True(options[FeatureKind.FrostAlert]),
            () => Assert.False(options[FeatureKind.HeatAlert]));
    }

    [Fact]
    public void Should_only_hold_explicit_entries_without_legacy_keys()
    {
        var options = Bind(Config(("Njord:Features:FloodRisk", "true")));

        Assert.Equal(new KeyValuePair<FeatureKind, bool>(FeatureKind.FloodRisk, true), Assert.Single(options));
    }
}
