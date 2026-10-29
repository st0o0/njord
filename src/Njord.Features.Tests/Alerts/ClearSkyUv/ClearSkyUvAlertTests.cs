using Microsoft.Extensions.Options;
using Njord.Features.Alerts.ClearSkyUv;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.ClearSkyUv;

public sealed class ClearSkyUvAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static AlertSeverity Severity(double uvClearSky, double isDay = 1.0)
    {
        var rule = new ClearSkyUvAlert(Opts(new ClearSkyUvConfig { Enabled = true }));
        var weather = Uniform(p => p with { UvIndexClearSky = uvClearSky, IsDay = isDay });
        var result = Run(rule, Now, ForecastOnly(weather));
        return result is null
            ? AlertSeverity.None
            : Assert.IsAssignableFrom<AlertFeatureResult>(result).Severity;
    }

    [Theory]
    [InlineData(5.9, AlertSeverity.None)]
    [InlineData(6.0, AlertSeverity.Yellow)]
    [InlineData(8.0, AlertSeverity.Orange)]
    [InlineData(10.0, AlertSeverity.Red)]
    public void Clear_sky_uv_severity_follows_peak(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(value));

    [Fact]
    public void Nighttime_hours_excluded()
    {
        var rule = new ClearSkyUvAlert(Opts(new ClearSkyUvConfig { Enabled = true }));
        var weather = Uniform(p => p with { UvIndexClearSky = 12.0, IsDay = 0.0 });
        var result = Run(rule, Now, ForecastOnly(weather));
        Assert.Null(result);
    }

    [Fact]
    public void Confidence_is_always_medium()
    {
        var rule = new ClearSkyUvAlert(Opts(new ClearSkyUvConfig { Enabled = true }));
        var weather = Uniform(p => p with { UvIndexClearSky = 9.0, IsDay = 1.0 });
        var result = Run(rule, Now, ForecastOnly(weather));
        var alert = Assert.IsType<ClearSkyUvAlertResult>(result);
        Assert.Equal(ConfidenceLevel.Medium, alert.Level);
    }

    [Fact]
    public void No_ensemble_required()
    {
        var rule = new ClearSkyUvAlert(Opts(new ClearSkyUvConfig { Enabled = true }));
        var weather = Uniform(p => p with { UvIndexClearSky = 9.0, IsDay = 1.0 });
        var result = Run(rule, Now, ForecastOnly(weather));
        Assert.NotNull(result);
    }
}
