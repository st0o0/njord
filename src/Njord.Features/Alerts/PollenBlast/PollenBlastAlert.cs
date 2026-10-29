using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.PollenBlast;

public sealed class PollenBlastAlert(IOptions<PollenBlastConfig> config) : FeatureRule<ForecastAqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.AirQuality,
        Name: "PollenBlast",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.WindSpeed | ForecastParams.Precipitation,
            AirQuality: AirQualityParams.GrassPollen | AirQualityParams.BirchPollen),
        PollDepth: 8);

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        if (ctx.History.Count < 3)
        {
            return null;
        }

        var hadRain = false;
        for (var i = ctx.History.Count - 1; i >= 1; i--)
        {
            var past = ctx.History[i];
            if (past.Forecast.Points.Any(p => p.Precipitation is > 0 && p.Precipitation.Value >= cfg.PrecipitationThreshold))
            {
                hadRain = true;
                break;
            }
        }

        if (!hadRain)
        {
            return null;
        }

        var currentWeather = ctx.Current.Forecast.NextHoursInclusive(ctx.Now, 12);
        var isDry = currentWeather.All(p => p.Precipitation is null or < 0.1);
        var isWarm = currentWeather.Any(p => p.Temperature >= cfg.TemperatureThreshold);
        var isWindy = currentWeather.Any(p => p.WindSpeed >= cfg.WindSpeedThreshold);

        if (!isDry || !isWarm || !isWindy)
        {
            return null;
        }

        var currentAq = ctx.Current.AirQuality.NextHoursInclusive(ctx.Now, 12);
        var currentPollen = MaxPollen(currentAq);

        double? previousPollen = null;
        if (ctx.History.Count >= 2)
        {
            var prevAq = ctx.History[1].AirQuality;
            if (prevAq is not null)
            {
                previousPollen = MaxPollen(prevAq.NextHoursInclusive(ctx.Now, 12));
            }
        }

        if (currentPollen <= 0 || (previousPollen is not null && currentPollen - previousPollen.Value < cfg.PollenDelta))
        {
            return null;
        }

        var severity = currentPollen switch
        {
            >= 100 => AlertSeverity.Red,
            >= 50 => AlertSeverity.Orange,
            _ => AlertSeverity.Yellow,
        };

        return new PollenBlastAlertResult(
            ctx.Location, severity, ConfidenceLevel.Medium.ToConfidence(),
            Math.Round(currentPollen, 1), cfg.PollenDelta,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: ConfidenceLevel.Medium,
            PeakPollen: Math.Round(currentPollen, 1));
    }

    private static double MaxPollen(IReadOnlyList<AirQualityPoint> points)
    {
        double max = 0;
        foreach (var p in points)
        {
            if (p.BirchPollen is { } b && b > max)
            {
                max = b;
            }

            if (p.GrassPollen is { } g && g > max)
            {
                max = g;
            }
        }

        return max;
    }
}
