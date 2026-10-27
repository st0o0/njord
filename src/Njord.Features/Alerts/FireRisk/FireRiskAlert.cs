using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.FireRisk;

public sealed class FireRiskAlert(IOptions<FireRiskConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Lifestyle,
        Name: "FireRisk",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.Humidity | ForecastParams.WindSpeed
                | ForecastParams.SoilMoisture0To1cm | ForecastParams.Precipitation,
            Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var cfg = config.Value;
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
        if (window.Count == 0)
        {
            return null;
        }

        double tempScore = 0, humidScore = 0, windScore = 0, soilScore = 0, dryScore = 0;
        int count = 0;

        foreach (var p in window)
        {
            if (p.Temperature is null || p.Humidity is null || p.WindSpeed is null)
            {
                continue;
            }

            count++;
            if (p.Temperature.Value >= cfg.TemperatureThreshold)
            {
                tempScore += 1.0;
            }
            else if (p.Temperature.Value >= cfg.TemperatureThreshold - 5)
            {
                tempScore += (p.Temperature.Value - (cfg.TemperatureThreshold - 5)) / 5.0;
            }

            if (p.Humidity.Value <= cfg.HumidityThreshold)
            {
                humidScore += 1.0;
            }
            else if (p.Humidity.Value <= cfg.HumidityThreshold + 20)
            {
                humidScore += 1.0 - (p.Humidity.Value - cfg.HumidityThreshold) / 20.0;
            }

            if (p.WindSpeed.Value >= cfg.WindSpeedThreshold)
            {
                windScore += 1.0;
            }
            else if (p.WindSpeed.Value >= cfg.WindSpeedThreshold * 0.5)
            {
                windScore += (p.WindSpeed.Value - cfg.WindSpeedThreshold * 0.5) / (cfg.WindSpeedThreshold * 0.5);
            }

            if (p.SoilMoisture0To1cm is { } moisture && moisture <= cfg.SoilMoistureThreshold)
            {
                soilScore += 1.0;
            }
            else if (p.SoilMoisture0To1cm is { } m && m <= cfg.SoilMoistureThreshold * 2)
            {
                soilScore += 1.0 - (m - cfg.SoilMoistureThreshold) / cfg.SoilMoistureThreshold;
            }

            if (p.Precipitation is { } precip && precip <= 0.0)
            {
                dryScore += 1.0;
            }
        }

        if (count == 0)
        {
            return null;
        }

        tempScore /= count;
        humidScore /= count;
        windScore /= count;
        soilScore /= count;
        dryScore /= count;

        var totalScore = tempScore * 0.25 + humidScore * 0.25 + windScore * 0.20 + soilScore * 0.20 + dryScore * 0.10;
        var severity = totalScore switch
        {
            var s when s >= cfg.RedThreshold => AlertSeverity.Red,
            var s when s >= cfg.OrangeThreshold => AlertSeverity.Orange,
            var s when s >= cfg.YellowThreshold => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var hottest = window.HighestBy(p => p.Temperature);
        var spread = hottest is not null ? ctx.Current.Ensemble.Points.SpreadAt(hottest.ValidFrom) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new FireRiskAlertResult(
            ctx.Location, severity, level.ToConfidence(),
            Math.Round(totalScore, 2), cfg.YellowThreshold,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: level,
            RiskScore: Math.Round(totalScore, 2));
    }
}
