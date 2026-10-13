using Njord.Compute.Analysis;
using Njord.Core.Configuration.Indices;
using Njord.Weather;

namespace Njord.Core.Tests.Configuration.Indices;

public sealed class PreferenceResolverSpec
{
    private static IndexOptions DefaultOptions() => new();

    [Fact]
    public void Default_options_resolve_to_default_preferences()
    {
        var resolved = PreferenceResolver.Resolve(DefaultOptions(), ["Lucerne"]);
        var prefs = resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)];

        Assert.Equal(ResolvedPreferences.Default, prefs);
    }

    [Fact]
    public void Global_preferences_override_hardcoded_defaults()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { IdealOutdoorTemp = 26.0 }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);
        var prefs = resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)];

        Assert.Equal(26.0, prefs.IdealTemp);
        Assert.Equal(1.0, prefs.HeatSensitivity);
    }

    [Fact]
    public void Score_override_beats_global_preference()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { HeatSensitivity = 1.0 },
            ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
            {
                ["Outdoor"] = new IndexPreferences { HeatSensitivity = 1.5 }
            }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);

        Assert.Equal(1.5, resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)].HeatSensitivity);
        Assert.Equal(1.0, resolved[(new LocationId("Lucerne"), IndexKind.Running)].HeatSensitivity);
    }

    [Fact]
    public void Location_global_preference_beats_score_override()
    {
        var options = new IndexOptions
        {
            ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
            {
                ["Outdoor"] = new IndexPreferences { HeatSensitivity = 1.5 }
            },
            LocationOverrides =
            [
                new LocationIndexOverride
                {
                    Location = "Lucerne",
                    Preferences = new IndexPreferences { HeatSensitivity = 0.5 }
                }
            ]
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);

        Assert.Equal(0.5, resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)].HeatSensitivity);
    }

    [Fact]
    public void Location_score_override_wins_over_all()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { HeatSensitivity = 1.0 },
            ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
            {
                ["Outdoor"] = new IndexPreferences { HeatSensitivity = 1.5 }
            },
            LocationOverrides =
            [
                new LocationIndexOverride
                {
                    Location = "Lucerne",
                    Preferences = new IndexPreferences { HeatSensitivity = 0.5 },
                    ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Outdoor"] = new IndexPreferences { HeatSensitivity = 2.0 }
                    }
                }
            ]
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);

        Assert.Equal(2.0, resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)].HeatSensitivity);
    }

    [Fact]
    public void Unset_properties_fall_through_cascade()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { HeatSensitivity = 1.3 },
            LocationOverrides =
            [
                new LocationIndexOverride
                {
                    Location = "Lucerne",
                    ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Outdoor"] = new IndexPreferences { WindSensitivity = 0.8 }
                    }
                }
            ]
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);
        var prefs = resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)];

        Assert.Equal(1.3, prefs.HeatSensitivity);
        Assert.Equal(0.8, prefs.WindSensitivity);
    }

    [Fact]
    public void Location_matching_is_case_insensitive()
    {
        var options = new IndexOptions
        {
            LocationOverrides =
            [
                new LocationIndexOverride
                {
                    Location = "LUCERNE",
                    Preferences = new IndexPreferences { IdealOutdoorTemp = 25.0 }
                }
            ]
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);

        Assert.Equal(25.0, resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)].IdealTemp);
    }

    [Fact]
    public void Sensitivity_is_clamped_to_range()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { HeatSensitivity = 8.0, WindSensitivity = -1.0 }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);
        var prefs = resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)];

        Assert.Equal(5.0, prefs.HeatSensitivity);
        Assert.Equal(0.0, prefs.WindSensitivity);
    }

    [Fact]
    public void NightVentilation_key_resolves_preferences()
    {
        var options = new IndexOptions
        {
            ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
            {
                ["NightVentilation"] = new IndexPreferences { HumiditySensitivity = 1.5 }
            }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);

        Assert.Equal(1.5, resolved[(new LocationId("Lucerne"), IndexKind.NightVentilation)].HumiditySensitivity);
    }

    [Fact]
    public void Resolution_covers_every_index_kind_per_location()
    {
        var resolved = PreferenceResolver.Resolve(DefaultOptions(), ["Lucerne", "Bern"]);

        Assert.Equal(16, resolved.Count);
        Assert.True(resolved.ContainsKey((new LocationId("Lucerne"), IndexKind.NightVentilation)));
    }

    [Fact]
    public void Lowercase_score_key_applies_to_its_index_kind()
    {
        var options = new IndexOptions
        {
            ScoreOverrides = new Dictionary<string, IndexPreferences>
            {
                ["running"] = new IndexPreferences { RunningIdealTempLow = 8.0 }
            }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);

        Assert.Equal(8.0, resolved[(new LocationId("Lucerne"), IndexKind.Running)].IdealTempLow);
        Assert.Equal(5.0, resolved[(new LocationId("Lucerne"), IndexKind.Cycling)].IdealTempLow);
    }

    [Fact]
    public void Location_key_is_case_insensitive()
    {
        var resolved = PreferenceResolver.Resolve(DefaultOptions(), ["Lucerne"]);

        Assert.True(resolved.ContainsKey((new LocationId("lucerne"), IndexKind.Outdoor)));
    }

    [Fact]
    public void Multiple_locations_resolve_independently()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { IdealOutdoorTemp = 22.0 },
            LocationOverrides =
            [
                new LocationIndexOverride
                {
                    Location = "Lucerne",
                    Preferences = new IndexPreferences { IdealOutdoorTemp = 24.0 }
                }
            ]
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne", "Zurich"]);

        Assert.Equal(24.0, resolved[(new LocationId("Lucerne"), IndexKind.Outdoor)].IdealTemp);
        Assert.Equal(22.0, resolved[(new LocationId("Zurich"), IndexKind.Outdoor)].IdealTemp);
    }
}
