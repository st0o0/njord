using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.FireRisk;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.FireRisk;

public sealed class FireRiskAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static FireRiskAlert Rule(FireRiskConfig? cfg = null) => new(Opts(cfg ?? new FireRiskConfig { Enabled = true }));

    private static PipelineEvent? Run(DomainForecast<ForecastPoint> weather, FireRiskConfig? cfg = null) =>
        FeatureRunner.Run(Rule(cfg), Now, ForecastWithEnsemble(weather, EnsembleDay()));

    [Fact]
    public void All_conditions_met_produces_red_severity()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 38.0,
            Humidity = 15.0,
            WindSpeed = 8.0,
            SoilMoisture0To1cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Moderate_conditions_produces_yellow()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 33.0,
            Humidity = 20.0,
            WindSpeed = 5.0,
            SoilMoisture0To1cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.True(alert.Severity is AlertSeverity.Yellow or AlertSeverity.Orange or AlertSeverity.Red,
            $"Expected Yellow/Orange/Red but got {alert.Severity} (score={alert.RiskScore})");
    }

    [Fact]
    public void Adequate_moisture_produces_no_alert()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 25.0,
            Humidity = 60.0,
            WindSpeed = 2.0,
            SoilMoisture0To1cm = 0.30,
            Precipitation = 5.0,
        });
        var result = Run(weather);
        Assert.Null(result);
    }

    [Fact]
    public void Confidence_comes_from_ensemble()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 38.0,
            Humidity = 15.0,
            WindSpeed = 8.0,
            SoilMoisture0To1cm = 0.05,
            Precipitation = 0.0,
        });
        var ensemble = EnsembleDay(spread: 0.5);
        var result = FeatureRunner.Run(Rule(), Now, ForecastWithEnsemble(weather, ensemble));
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(ConfidenceLevel.High, alert.Level);
    }
}
