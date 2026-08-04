using Njord.Configuration;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class PreferenceResolverSpec
{
    private static IndexOptions DefaultOptions() => new();

    [Fact(Timeout = 5000)]
    public void Default_options_resolve_to_default_preferences()
    {
        var resolved = PreferenceResolver.Resolve(DefaultOptions(), ["Lucerne"]);
        var prefs = resolved[("Lucerne", "Outdoor")];

        Assert.Equal(ResolvedPreferences.Default, prefs);
    }

    [Fact(Timeout = 5000)]
    public void Global_preferences_override_hardcoded_defaults()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { IdealOutdoorTemp = 26.0 }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);
        var prefs = resolved[("Lucerne", "Outdoor")];

        Assert.Equal(26.0, prefs.IdealTemp);
        Assert.Equal(1.0, prefs.HeatSensitivity);
    }

    [Fact(Timeout = 5000)]
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

        Assert.Equal(1.5, resolved[("Lucerne", "Outdoor")].HeatSensitivity);
        Assert.Equal(1.0, resolved[("Lucerne", "Running")].HeatSensitivity);
    }

    [Fact(Timeout = 5000)]
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

        Assert.Equal(0.5, resolved[("Lucerne", "Outdoor")].HeatSensitivity);
    }

    [Fact(Timeout = 5000)]
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

        Assert.Equal(2.0, resolved[("Lucerne", "Outdoor")].HeatSensitivity);
    }

    [Fact(Timeout = 5000)]
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
        var prefs = resolved[("Lucerne", "Outdoor")];

        Assert.Equal(1.3, prefs.HeatSensitivity);
        Assert.Equal(0.8, prefs.WindSensitivity);
    }

    [Fact(Timeout = 5000)]
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

        Assert.Equal(25.0, resolved[("Lucerne", "Outdoor")].IdealTemp);
    }

    [Fact(Timeout = 5000)]
    public void Sensitivity_is_clamped_to_range()
    {
        var options = new IndexOptions
        {
            Preferences = new IndexPreferences { HeatSensitivity = 8.0, WindSensitivity = -1.0 }
        };

        var resolved = PreferenceResolver.Resolve(options, ["Lucerne"]);
        var prefs = resolved[("Lucerne", "Outdoor")];

        Assert.Equal(5.0, prefs.HeatSensitivity);
        Assert.Equal(0.0, prefs.WindSensitivity);
    }

    [Fact(Timeout = 5000)]
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

        Assert.Equal(24.0, resolved[("Lucerne", "Outdoor")].IdealTemp);
        Assert.Equal(22.0, resolved[("Zurich", "Outdoor")].IdealTemp);
    }
}
