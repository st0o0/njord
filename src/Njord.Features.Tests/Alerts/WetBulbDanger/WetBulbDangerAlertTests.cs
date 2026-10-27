using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.WetBulbDanger;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.WetBulbDanger;

public sealed class WetBulbDangerAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static AlertSeverity Severity(double wetBulb)
    {
        var rule = new WetBulbDangerAlert(Opts(new WetBulbDangerConfig { Enabled = true }));
        var weather = Uniform(p => p with { WetBulbTemperature = wetBulb });
        var result = Run(rule, Now, WeatherWithEnsemble(weather, EnsembleDay()));
        return result is null
            ? AlertSeverity.None
            : Assert.IsAssignableFrom<AlertFeatureResult>(result).Severity;
    }

    [Theory]
    [InlineData(25.9, AlertSeverity.None)]
    [InlineData(26.0, AlertSeverity.Yellow)]
    [InlineData(28.0, AlertSeverity.Orange)]
    [InlineData(32.0, AlertSeverity.Red)]
    public void Wet_bulb_severity_follows_peak_temperature(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(value));

    [Fact]
    public void Returns_null_when_no_wet_bulb()
    {
        var rule = new WetBulbDangerAlert(Opts(new WetBulbDangerConfig { Enabled = true }));
        var weather = Uniform(p => p with { WetBulbTemperature = null });
        var result = Run(rule, Now, WeatherWithEnsemble(weather, EnsembleDay()));
        Assert.Null(result);
    }

    [Fact]
    public void Result_carries_peak_wet_bulb()
    {
        var rule = new WetBulbDangerAlert(Opts(new WetBulbDangerConfig { Enabled = true }));
        var weather = Uniform(p => p with { WetBulbTemperature = 30.5 });
        var result = Run(rule, Now, WeatherWithEnsemble(weather, EnsembleDay()));
        var alert = Assert.IsType<WetBulbDangerAlertResult>(result);
        Assert.Equal(30.5, alert.PeakWetBulb);
    }
}
