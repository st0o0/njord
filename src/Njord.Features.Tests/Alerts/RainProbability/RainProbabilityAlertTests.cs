using Microsoft.Extensions.Options;
using Njord.Features.Alerts.RainProbability;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.RainProbability;

public sealed class RainProbabilityAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static AlertSeverity Severity(double probability)
    {
        var rule = new RainProbabilityAlert(Opts(new RainProbabilityConfig()));
        var weather = Uniform(p => p with { PrecipitationProbability = probability });
        var result = Run(rule, Now, ForecastWithEnsemble(weather, EnsembleDay()));
        return result is null
            ? AlertSeverity.None
            : Assert.IsAssignableFrom<AlertFeatureResult>(result).Severity;
    }

    [Theory]
    [InlineData(49.9, AlertSeverity.None)]
    [InlineData(50.0, AlertSeverity.Yellow)]
    [InlineData(70.0, AlertSeverity.Orange)]
    [InlineData(90.0, AlertSeverity.Red)]
    public void Rain_probability_severity_follows_peak_probability(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(value));

    [Fact]
    public void Returns_null_when_no_precipitation_probability()
    {
        var rule = new RainProbabilityAlert(Opts(new RainProbabilityConfig()));
        var weather = Uniform(p => p with { PrecipitationProbability = null });
        var result = Run(rule, Now, ForecastWithEnsemble(weather, EnsembleDay()));
        Assert.Null(result);
    }

    [Fact]
    public void Custom_window_hours_limits_scan()
    {
        var config = new RainProbabilityConfig { WindowHours = 3 };
        var rule = new RainProbabilityAlert(Opts(config));
        var weather = Hourly(24, (hour, p) => p with { PrecipitationProbability = hour > 3 ? 95.0 : 10.0 });
        var result = Run(rule, Now, ForecastWithEnsemble(weather, EnsembleDay()));
        Assert.Null(result);
    }

    [Fact]
    public void Missing_ensemble_skips()
    {
        var rule = new RainProbabilityAlert(Opts(new RainProbabilityConfig()));
        var weather = Uniform(p => p with { PrecipitationProbability = 90.0 });
        var result = Run(rule, Now, ForecastOnly(weather));
        Assert.Null(result);
    }

    [Fact]
    public void Result_carries_peak_probability()
    {
        var rule = new RainProbabilityAlert(Opts(new RainProbabilityConfig()));
        var weather = Uniform(p => p with { PrecipitationProbability = 85.0 });
        var result = Run(rule, Now, ForecastWithEnsemble(weather, EnsembleDay()));
        var alert = Assert.IsType<RainProbabilityAlertResult>(result);
        Assert.Equal(85.0, alert.PeakProbability);
    }
}
