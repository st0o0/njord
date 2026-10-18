using Njord.Indices.Configuration;
using Njord.Tests.Shared;

namespace Njord.Indices.Tests.Configuration;

public sealed class IndexOptionsValidatorSpec
{
    private static IndexOptions WithScoreOverride(string key)
    {
        var options = AppSettingsDefaults.Options<IndexOptions>();
        options.ScoreOverrides[key] = new IndexPreferences { HeatSensitivity = 1.5 };
        return options;
    }

    [Fact]
    public void Default_options_are_valid()
    {
        var result = new IndexOptionsValidator().Validate(null, AppSettingsDefaults.Options<IndexOptions>());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Running")]
    [InlineData("running")]
    [InlineData("NightVentilation")]
    public void Known_score_override_key_is_accepted(string key)
    {
        var result = new IndexOptionsValidator().Validate(null, WithScoreOverride(key));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Unknown_global_score_override_key_fails_naming_the_key()
    {
        var result = new IndexOptionsValidator().Validate(null, WithScoreOverride("Swimming"));

        Assert.True(result.Failed);
        Assert.Contains("Indices.ScoreOverrides", result.FailureMessage);
        Assert.Contains("Swimming", result.FailureMessage);
    }

    [Fact]
    public void Unknown_location_score_override_key_fails_naming_location_and_key()
    {
        var options = AppSettingsDefaults.Options<IndexOptions>();
        options.LocationOverrides.Add(new LocationIndexOverride
        {
            Location = "Lucerne",
            ScoreOverrides = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ventilation"] = new IndexPreferences(),
            },
        });

        var result = new IndexOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Indices.LocationOverrides[Lucerne].ScoreOverrides", result.FailureMessage);
        Assert.Contains("Ventilation", result.FailureMessage);
    }

    [Fact]
    public void Out_of_range_sensitivity_still_fails()
    {
        var options = AppSettingsDefaults.Options<IndexOptions>();
        options.Preferences.WindSensitivity = 6.0;

        var result = new IndexOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("WindSensitivity", result.FailureMessage);
    }
}
