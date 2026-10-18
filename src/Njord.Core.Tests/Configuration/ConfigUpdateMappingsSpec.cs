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

namespace Njord.Core.Tests.Configuration;

public sealed class ConfigUpdateMappingsSpec
{
    [Fact]
    public void Polling_options_map_to_the_polling_changed_message()
    {
        var message = new PollingOptions
        {
            PollInterval = TimeSpan.FromMinutes(30),
            DiscoveryInterval = TimeSpan.FromMinutes(10),
        }.ToMessage();

        Assert.Equal(new PollingChanged(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10)), message);
    }

    [Fact]
    public void An_empty_analysis_command_builds_no_updates()
    {
        var updates = new UpdateAnalysisConfig(null, null, null, null, null).ToAnalysisUpdates();

        Assert.Empty(updates);
    }

    [Fact]
    public void Touched_sections_become_updates_with_their_prefix_in_section_order()
    {
        var command = new UpdateAnalysisConfig(
            new ConsensusConfigUpdate(false, null, null),
            null,
            true,
            null,
            new IndexConfigUpdate(true, null, null, null, null, null, null, null, null, null, null, null));

        var updates = command.ToAnalysisUpdates();

        Assert.Multiple(
            () => Assert.Equal(
                [EnrichmentSection.Consensus, EnrichmentSection.Derived, EnrichmentSection.Indices],
                updates.Select(u => u.Section)),
            () => Assert.Equal(
                ["Enrichment.Consensus.", "Enrichment.Derived.", "Enrichment.Indices."],
                updates.Select(u => u.Prefix)));
    }

    [Fact]
    public void A_reset_section_without_a_change_still_becomes_an_update()
    {
        var command = new UpdateAnalysisConfig(null, null, null, null, null, [EnrichmentSection.Trends]);

        var update = Assert.Single(command.ToAnalysisUpdates());

        Assert.Multiple(
            () => Assert.Equal(EnrichmentSection.Trends, update.Section),
            () => Assert.Equal("Enrichment.Trends.", update.Prefix));
    }

    [Fact]
    public void Consensus_update_changes_only_the_given_values()
    {
        var options = new ConsensusOptions();

        new ConsensusConfigUpdate(false, ConsensusMethod.Mean, null).ApplyTo(options);

        Assert.Multiple(
            () => Assert.False(options.Enabled),
            () => Assert.Equal(ConsensusMethod.Mean, options.Method),
            () => Assert.Equal(0.1, options.TrimPercent));
    }

    [Fact]
    public void Alert_update_applies_every_given_value_and_leaves_the_rest()
    {
        var options = new AlertOptions();

        new AlertConfigUpdate(
            false, [1], [2], [3], 4, 5, 6, 7, 8, 9, 10, 11, 12, [13], [14], [15], [16]).ApplyTo(options);

        Assert.Multiple(
            () => Assert.False(options.Enabled),
            () => Assert.Equal([1], options.FrostThresholds),
            () => Assert.Equal([2], options.HeatThresholds),
            () => Assert.Equal([3], options.StormGustThresholds),
            () => Assert.Equal(4, options.HeavyRainHourlyThreshold),
            () => Assert.Equal(5, options.HeavyRainDailyThreshold),
            () => Assert.Equal(6, options.PressureDropThreshold),
            () => Assert.Equal(7, options.CapeThreshold),
            () => Assert.Equal(8, options.ThunderstormPrecipThreshold),
            () => Assert.Equal(9, options.ThunderstormGustThreshold),
            () => Assert.Equal(10, options.PressureDropSevereThreshold),
            () => Assert.Equal(11, options.FogPersistentHours),
            () => Assert.Equal(12, options.IceThreshold),
            () => Assert.Equal([13], options.WindChillThresholds),
            () => Assert.Equal([14], options.VisibilityThresholds),
            () => Assert.Equal([15], options.TropicalNightThresholds),
            () => Assert.Equal([16], options.HumidityThresholds));
    }

    [Fact]
    public void An_empty_alert_update_leaves_the_options_unchanged()
    {
        var options = new AlertOptions();

        new AlertConfigUpdate(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null).ApplyTo(options);

        Assert.Equal(new AlertOptions().FrostThresholds, options.FrostThresholds);
        Assert.Equal(new AlertOptions().IceThreshold, options.IceThreshold);
    }

    [Fact]
    public void Index_update_applies_every_given_value()
    {
        var options = new IndexOptions();

        new IndexConfigUpdate(true, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11).ApplyTo(options);

        var preferences = options.Preferences;
        Assert.Multiple(
            () => Assert.True(options.Enabled),
            () => Assert.Equal(1, preferences.IndoorTemp),
            () => Assert.Equal(2, preferences.IdealOutdoorTemp),
            () => Assert.Equal(3, preferences.HeatSensitivity),
            () => Assert.Equal(4, preferences.HumiditySensitivity),
            () => Assert.Equal(5, preferences.WindSensitivity),
            () => Assert.Equal(6, preferences.RainSensitivity),
            () => Assert.Equal(7, preferences.RunningIdealTempLow),
            () => Assert.Equal(8, preferences.RunningIdealTempHigh),
            () => Assert.Equal(9, preferences.BbqMinTemp),
            () => Assert.Equal(10, preferences.BbqIdealWindLow),
            () => Assert.Equal(11, preferences.BbqIdealWindHigh));
    }

    [Fact]
    public void Settings_command_applies_days_horizons_models_and_parameters()
    {
        var options = new NjordOptions { Models = ["icon_d2"] };

        new UpdateSettings(7, [6, 12], ["icon_eu"], new ParameterSelection(["Weather"], ["uv_index"], ["snowfall"]), null)
            .ApplyTo(options);

        Assert.Multiple(
            () => Assert.Equal(7, options.ForecastDays),
            () => Assert.Equal([6, 12], options.Horizons),
            () => Assert.Equal(["icon_eu"], options.Models),
            () => Assert.Equal(["Weather"], options.Parameters.Groups),
            () => Assert.Equal(["uv_index"], options.Parameters.Extra),
            () => Assert.Equal(["snowfall"], options.Parameters.Exclude));
    }

    [Fact]
    public void An_empty_settings_command_leaves_the_scope_unchanged()
    {
        var options = new NjordOptions { Models = ["icon_d2"], ForecastDays = 4 };

        new UpdateSettings(null, [], [], null, null).ApplyTo(options);

        Assert.Multiple(
            () => Assert.Equal(4, options.ForecastDays),
            () => Assert.Equal([3, 6, 12, 24, 48, 72], options.Horizons),
            () => Assert.Equal(["icon_d2"], options.Models),
            () => Assert.Equal(["Weather"], options.Parameters.Groups));
    }
}
