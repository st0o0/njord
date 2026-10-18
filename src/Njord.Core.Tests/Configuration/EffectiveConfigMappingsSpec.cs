using Njord.Compute.Settings;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Njord.Messages.Config;
using Njord.Weather;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class EffectiveConfigMappingsSpec
{
    [Fact]
    public void Consensus_options_map_to_their_section()
    {
        var section = new ConsensusOptions
        {
            Enabled = false,
            Method = ConsensusMethod.TrimmedMean,
            TrimPercent = 0.2,
            MinQuorum = 3,
        }.ToSection();

        Assert.Equal(new ConsensusSection(false, ConsensusMethod.TrimmedMean, 0.2, 3), section);
    }

    [Fact]
    public void Derived_and_trend_options_map_their_enabled_flag()
    {
        Assert.Multiple(
            () => Assert.Equal(new DerivedSection(false), AppSettingsDefaults.Options<DerivedOptions>(o => o.Enabled = false).ToSection()),
            () => Assert.Equal(new TrendSection(false), AppSettingsDefaults.Options<TrendOptions>(o => o.Enabled = false).ToSection()));
    }

    [Fact]
    public void Alert_options_map_every_field()
    {
        var options = new AlertOptions
        {
            Enabled = false,
            FrostThresholds = [1, 2],
            HeatThresholds = [3, 4],
            StormGustThresholds = [5, 6],
            HeavyRainHourlyThreshold = 7,
            HeavyRainDailyThreshold = 8,
            PressureDropThreshold = 9,
            PressureDropSevereThreshold = 10,
            FogPersistentHours = 11,
            CapeThreshold = 12,
            ThunderstormPrecipThreshold = 13,
            ThunderstormGustThreshold = 14,
            IceThreshold = 15,
            WindChillThresholds = [16],
            VisibilityThresholds = [17],
            TropicalNightThresholds = [18],
            HumidityThresholds = [19],
        };

        var section = options.ToSection();

        Assert.Equal(
            new AlertSection(false, [1, 2], [3, 4], [5, 6], 7, 8, 9, 10, 11, 12, 13, 14, 15, [16], [17], [18], [19]),
            section,
            new AlertSectionComparer());
    }

    [Fact]
    public void Index_preferences_map_every_value()
    {
        var preferences = new IndexPreferences
        {
            IdealOutdoorTemp = 1,
            RunningIdealTempLow = 2,
            RunningIdealTempHigh = 3,
            BbqMinTemp = 4,
            BbqIdealWindLow = 5,
            BbqIdealWindHigh = 6,
            IndoorTemp = 7,
            HeatSensitivity = 8,
            HumiditySensitivity = 9,
            WindSensitivity = 10,
            RainSensitivity = 11,
        };

        Assert.Equal(new IndexPreferenceValues(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11), preferences.ToValues());
    }

    [Fact]
    public void Score_overrides_keep_their_keys_case_insensitively()
    {
        var overrides = new Dictionary<string, IndexPreferences> { ["Running"] = new() { BbqMinTemp = 4 } };

        var values = overrides.ToValues();

        Assert.Equal(4, values["running"].BbqMinTemp);
    }

    [Fact]
    public void Index_options_map_enabled_preferences_overrides_and_location_overrides()
    {
        var options = new IndexOptions
        {
            Enabled = true,
            Preferences = new IndexPreferences { IndoorTemp = 21 },
            ScoreOverrides = new Dictionary<string, IndexPreferences> { ["bbq"] = new() { BbqMinTemp = 12 } },
            LocationOverrides =
            [
                new LocationIndexOverride
                {
                    Location = "lucerne",
                    Preferences = new IndexPreferences { WindSensitivity = 2 },
                },
            ],
        };

        var section = options.ToSection();

        Assert.Multiple(
            () => Assert.True(section.Enabled),
            () => Assert.Equal(21, section.Preferences.IndoorTemp),
            () => Assert.Equal(12, section.ScoreOverrides["bbq"].BbqMinTemp),
            () => Assert.Equal("lucerne", Assert.Single(section.LocationOverrides).Location),
            () => Assert.Equal(2, section.LocationOverrides[0].Preferences.WindSensitivity));
    }

    [Fact]
    public void Polling_options_map_intervals_override_and_effective_budget()
    {
        var polling = new PollingOptions
        {
            PollInterval = TimeSpan.FromMinutes(30),
            DiscoveryInterval = TimeSpan.FromMinutes(10),
            BudgetOverride = new RequestBudget(50_000, 60),
        };

        var section = polling.ToSection();

        Assert.Equal(
            new PollingSection(
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(10),
                new BudgetLimits(50_000, 60),
                new BudgetLimits(50_000, 60),
                RequestBudget.OpenMeteoFreeTierDailyLimit),
            section);
    }

    [Fact]
    public void Polling_options_without_override_report_the_free_tier_as_effective_budget()
    {
        var section = AppSettingsDefaults.Options<PollingOptions>().ToSection();

        Assert.Multiple(
            () => Assert.Null(section.BudgetOverride),
            () => Assert.Equal(new BudgetLimits(300_000, 600), section.EffectiveBudget));
    }

    [Fact]
    public void Scope_resolves_the_models_of_each_location()
    {
        var scope = new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31, Models = ["icon_d2", "ICON_EU"] },
                new LocationOptions { Name = "Bern", Latitude = 46.95, Longitude = 7.44 },
            ],
            Models = ["icon_eu", "icon_global"],
            Horizons = [3, 6],
            ForecastDays = 5,
            Parameters = new ParameterOptions { Groups = ["Weather"], Extra = ["uv_index"], Exclude = ["snowfall"] },
        };

        var mapped = scope.ToScope();

        Assert.Multiple(
            () => Assert.Equal(2, mapped.Locations.Count),
            () => Assert.Equal(new LocationId("Lucerne"), mapped.Locations[0].Id),
            () => Assert.Equal(47.05, mapped.Locations[0].Latitude),
            () => Assert.Equal(8.31, mapped.Locations[0].Longitude),
            () => Assert.Equal(["icon_d2", "ICON_EU"], mapped.Locations[0].Models),
            () => Assert.Equal(["icon_eu", "icon_global", "icon_d2"], mapped.Locations[0].ResolvedModels),
            () => Assert.Empty(mapped.Locations[1].Models),
            () => Assert.Equal(["icon_eu", "icon_global"], mapped.Locations[1].ResolvedModels),
            () => Assert.Equal(["icon_eu", "icon_global"], mapped.Models),
            () => Assert.Equal([3, 6], mapped.Horizons),
            () => Assert.Equal(5, mapped.ForecastDays),
            () => Assert.Equal(new ParameterScope(["Weather"], ["uv_index"], ["snowfall"]), mapped.Parameters, new ParameterScopeComparer()));
    }

    [Fact]
    public void Budget_validation_maps_to_the_projection()
    {
        var validation = new BudgetValidation(1000, 2000, 50, true, ["careful"], 3, 4);

        var projection = validation.ToProjection();

        Assert.Multiple(
            () => Assert.Equal(1000, projection.ProjectedMonthlyCalls),
            () => Assert.Equal(2000, projection.MonthlyLimit),
            () => Assert.Equal(50, projection.UsagePercent),
            () => Assert.True(projection.WithinBudget),
            () => Assert.Equal(["careful"], projection.Warnings));
    }

    [Fact]
    public void An_effective_config_combines_every_section()
    {
        var scope = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };

        var config = EffectiveConfigMappings.ToEffectiveConfig(
            scope,
            AppSettingsDefaults.Options<PollingOptions>(),
            AppSettingsDefaults.Options<ConsensusOptions>(o => o.MinQuorum = 4),
            AppSettingsDefaults.Options<AlertOptions>(),
            AppSettingsDefaults.Options<DerivedOptions>(),
            AppSettingsDefaults.Options<TrendOptions>(),
            AppSettingsDefaults.Options<IndexOptions>(o => o.Enabled = true));

        Assert.Multiple(
            () => Assert.Equal(new LocationId("Lucerne"), Assert.Single(config.Scope.Locations).Id),
            () => Assert.Equal(TimeSpan.FromMinutes(60), config.Polling.PollInterval),
            () => Assert.Equal(300_000, config.BudgetProjection.MonthlyLimit),
            () => Assert.Equal(4, config.Analysis.Consensus.MinQuorum),
            () => Assert.True(config.Analysis.Indices.Enabled));
    }

    [Fact]
    public void The_catalog_lists_locations_in_order_and_models_once_with_coverage()
    {
        var scope = new NjordOptions
        {
            Locations =
            [
                new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31, Models = ["icon_d2"] },
                new LocationOptions { Name = "Bern", Latitude = 46.95, Longitude = 7.44 },
            ],
            Models = ["icon_eu"],
        };

        var catalog = scope.ToCatalog();

        Assert.Multiple(
            () => Assert.Equal(["Lucerne", "Bern"], catalog.Locations.Select(l => l.Id.Name)),
            () => Assert.Equal(["icon_eu", "icon_d2"], catalog.Locations[0].Models),
            () => Assert.Equal(["icon_eu", "icon_d2"], catalog.Models.Select(m => m.Id)),
            () => Assert.Equal(ModelCoverageRegistry.Get("icon_d2"), catalog.Models[1].Coverage));
    }

    private sealed class AlertSectionComparer : IEqualityComparer<AlertSection>
    {
        public bool Equals(AlertSection? x, AlertSection? y) =>
            x is not null && y is not null
            && x.Enabled == y.Enabled
            && x.FrostThresholds.SequenceEqual(y.FrostThresholds)
            && x.HeatThresholds.SequenceEqual(y.HeatThresholds)
            && x.StormGustThresholds.SequenceEqual(y.StormGustThresholds)
            && x.HeavyRainHourlyThreshold == y.HeavyRainHourlyThreshold
            && x.HeavyRainDailyThreshold == y.HeavyRainDailyThreshold
            && x.PressureDropThreshold == y.PressureDropThreshold
            && x.PressureDropSevereThreshold == y.PressureDropSevereThreshold
            && x.FogPersistentHours == y.FogPersistentHours
            && x.CapeThreshold == y.CapeThreshold
            && x.ThunderstormPrecipThreshold == y.ThunderstormPrecipThreshold
            && x.ThunderstormGustThreshold == y.ThunderstormGustThreshold
            && x.IceThreshold == y.IceThreshold
            && x.WindChillThresholds.SequenceEqual(y.WindChillThresholds)
            && x.VisibilityThresholds.SequenceEqual(y.VisibilityThresholds)
            && x.TropicalNightThresholds.SequenceEqual(y.TropicalNightThresholds)
            && x.HumidityThresholds.SequenceEqual(y.HumidityThresholds);

        public int GetHashCode(AlertSection obj) => obj.Enabled.GetHashCode();
    }

    private sealed class ParameterScopeComparer : IEqualityComparer<ParameterScope>
    {
        public bool Equals(ParameterScope? x, ParameterScope? y) =>
            x is not null && y is not null
            && x.Groups.SequenceEqual(y.Groups)
            && x.Extra.SequenceEqual(y.Extra)
            && x.Exclude.SequenceEqual(y.Exclude);

        public int GetHashCode(ParameterScope obj) => obj.Groups.Count;
    }
}
