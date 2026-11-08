using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Shower;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.Shower;

public sealed class ShowerAlertTests
{
    private static ShowerAlert Rule(ShowerConfig? cfg = null)
    {
        var c = cfg ?? new ShowerConfig { Enabled = true };
        c.ApplyDefaults();
        return new(Options.Create(c));
    }

    [Fact]
    public void Heavy_shower_produces_red_severity()
    {
        var weather = Day((hour, p) => p with { Showers = hour == 2 ? 12.0 : 0.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ShowerAlertResult>(result);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Moderate_shower_produces_yellow_severity()
    {
        var weather = Day((hour, p) => p with { Showers = hour == 2 ? 3.5 : 0.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ShowerAlertResult>(result);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Below_threshold_returns_null()
    {
        var weather = Day((_, p) => p with { Showers = 1.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        Assert.Null(result);
    }

    [Fact]
    public void Returns_null_when_showers_data_missing()
    {
        var weather = Day();
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        Assert.Null(result);
    }
}
