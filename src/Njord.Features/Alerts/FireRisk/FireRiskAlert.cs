using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.FireRisk;

public sealed class FireRiskAlert(IOptions<FireRiskConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    private const double ColdCutoff = 15.0;
    private const double HumidCutoff = 70.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Lifestyle,
        Name: "FireRisk",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.Humidity | ForecastParams.WindSpeed
                | ForecastParams.SoilMoisture0To1cm | ForecastParams.Precipitation,
            OptionalForecast: ForecastParams.IsDay,
            Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var cfg = config.Value;
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now)
            .Where(p => p.Temperature is not null && p.Humidity is not null && p.WindSpeed is not null)
            .ToList();
        var daytime = window.Where(p => p.IsDay > 0).ToList();
        var scored = daytime.Count > 0 ? daytime : window;
        if (scored.Count == 0)
        {
            return null;
        }

        double weatherSum = 0, soilSum = 0, drySum = 0;
        int soilCount = 0, dryCount = 0;

        foreach (var p in scored)
        {
            weatherSum += WeatherScore(p.Temperature!.Value, p.Humidity!.Value, p.WindSpeed!.Value, cfg);

            if (p.SoilMoisture0To1cm is { } m)
            {
                soilCount++;
                soilSum += m <= cfg.SoilMoistureThreshold
                    ? 1.0
                    : m <= cfg.SoilMoistureThreshold * 2 ? 1.0 - (m - cfg.SoilMoistureThreshold) / cfg.SoilMoistureThreshold : 0.0;
            }

            if (p.Precipitation is { } precip)
            {
                dryCount++;
                if (precip <= 0.0)
                {
                    drySum += 1.0;
                }
            }
        }

        var weather = weatherSum / scored.Count;
        var soil = soilCount > 0 ? soilSum / soilCount : 0.5;
        var dry = dryCount > 0 ? drySum / dryCount : 0.5;
        var multiplier = 0.5 + soil * 0.7 + dry * 0.3;
        var totalScore = weather * multiplier;

        var (severity, threshold) = totalScore switch
        {
            var s when s >= cfg.RedThreshold => (AlertSeverity.Red, cfg.RedThreshold),
            var s when s >= cfg.OrangeThreshold => (AlertSeverity.Orange, cfg.OrangeThreshold),
            var s when s >= cfg.YellowThreshold => (AlertSeverity.Yellow, cfg.YellowThreshold),
            _ => (AlertSeverity.None, 0.0),
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var hottest = scored.HighestBy(p => p.Temperature);
        var spread = hottest is not null ? ctx.Current.Ensemble.Points.SpreadAt(hottest.ValidFrom) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new FireRiskAlertResult(
            ctx.Location, severity, level.ToConfidence(),
            Math.Round(totalScore, 2), threshold,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: level,
            RiskScore: Math.Round(totalScore, 2));
    }

    private static double WeatherScore(double temperature, double humidity, double windSpeed, FireRiskConfig cfg)
    {
        if (temperature < ColdCutoff || humidity > HumidCutoff)
        {
            return 0.0;
        }

        var temp = temperature >= cfg.TemperatureThreshold
            ? 1.0
            : Math.Max(0.0, (temperature - (cfg.TemperatureThreshold - 5)) / 5.0);
        var humid = humidity <= cfg.HumidityThreshold
            ? 1.0
            : Math.Max(0.0, 1.0 - (humidity - cfg.HumidityThreshold) / 20.0);
        if (temp == 0.0 || humid == 0.0)
        {
            return 0.0;
        }

        var wind = windSpeed >= cfg.WindSpeedThreshold
            ? 1.0
            : Math.Max(0.0, (windSpeed - cfg.WindSpeedThreshold * 0.5) / (cfg.WindSpeedThreshold * 0.5));
        return temp * 0.4 + humid * 0.4 + wind * 0.2;
    }
}
