using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Snow;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.Snow;

public sealed class SnowAlertTests
{
    private static SnowAlert Rule()
    {
        var c = new SnowConfig { Enabled = true };
        c.ApplyDefaults();
        return new(Options.Create(c));
    }

    [Fact]
    public void Heavy_snowfall_produces_red_severity()
    {
        var weather = Day((_, p) => p with { Snowfall = 1.0, FreezingLevelHeight = 800.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<SnowAlertResult>(result);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(24.0, alert.ExpectedAccumulation));
    }

    [Fact]
    public void Moderate_snowfall_produces_orange_severity()
    {
        var weather = Day((_, p) => p with { Snowfall = 0.3, FreezingLevelHeight = 1000.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<SnowAlertResult>(result);
        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Light_snowfall_produces_yellow_severity()
    {
        var weather = Day((_, p) => p with { Snowfall = 0.05, FreezingLevelHeight = 1200.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<SnowAlertResult>(result);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void No_snowfall_data_produces_null()
    {
        var weather = Day();
        Assert.Null(Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay())));
    }

    [Fact]
    public void Freezing_level_is_reported()
    {
        var weather = Day((_, p) => p with { Snowfall = 1.0, FreezingLevelHeight = 800.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<SnowAlertResult>(result);
        Assert.Equal(800.0, alert.FreezingLevel);
    }
}
