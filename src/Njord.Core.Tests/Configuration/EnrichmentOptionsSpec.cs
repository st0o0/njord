using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class EnrichmentOptionsSpec
{
    private readonly EnrichmentOptionsValidator _validator = new();

    [Fact]
    public void Should_enable_only_the_four_default_enrichments()
    {
        var enabled = new EnrichmentOptions().EnabledKeys();

        Assert.Equal(["CommuteSafety", "Pm25Alert", "PollenAlert", "WeatherAlerts"], enabled.Order());
    }

    [Theory]
    [InlineData("WeatherAlerts", true)]
    [InlineData("CommuteSafety", true)]
    [InlineData("PollenAlert", true)]
    [InlineData("Pm25Alert", true)]
    [InlineData("FloodRisk", false)]
    [InlineData("FloodRain", false)]
    [InlineData("SwimmingConditions", false)]
    [InlineData("SurfConditions", false)]
    [InlineData("VentilationWindow", false)]
    [InlineData("ExerciseAdvisory", false)]
    [InlineData("LaundryScore", false)]
    [InlineData("PollenTrend", false)]
    [InlineData("NightVentilation", false)]
    [InlineData("ConfidenceTrend", false)]
    public void Should_report_the_default_of_every_enrichment(string key, bool expected)
    {
        Assert.Equal(expected, new EnrichmentOptions().IsEnabled(key));
    }

    [Fact]
    public void Should_follow_a_changed_toggle()
    {
        var options = new EnrichmentOptions { FloodRisk = true, WeatherAlerts = false };

        Assert.Multiple(
            () => Assert.True(options.IsEnabled("FloodRisk")),
            () => Assert.False(options.IsEnabled("WeatherAlerts")));
    }

    [Fact]
    public void Should_reject_an_unknown_key()
    {
        var ex = Assert.Throws<ArgumentException>(() => new EnrichmentOptions().IsEnabled("Lava"));

        Assert.Equal("key", ex.ParamName);
        Assert.StartsWith("Unknown enrichment key 'Lava'.", ex.Message);
    }

    [Fact]
    public void Should_accept_the_defaults()
    {
        Assert.True(_validator.Validate(null, new EnrichmentOptions()).Succeeded);
    }

    [Fact]
    public void Should_reject_a_configuration_without_any_enabled_enrichment()
    {
        var options = new EnrichmentOptions
        {
            WeatherAlerts = false,
            CommuteSafety = false,
            PollenAlert = false,
            Pm25Alert = false,
        };

        var result = _validator.Validate(null, options);

        Assert.Equal("At least one enrichment must be enabled.", ValidationAssert.SingleFailure(result));
    }
}
