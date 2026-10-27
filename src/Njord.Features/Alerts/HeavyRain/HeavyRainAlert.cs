using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainAlert(IOptions<HeavyRainConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "HeavyRain",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Precipitation, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
    {
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        if (window.HighestBy(p => p.Precipitation) is not { Precipitation: { } peakPrecipitation } wettest)
        {
            return null;
        }

        var hourlyThreshold = config.Value.HourlyThreshold;
        var dailyThreshold = config.Value.DailyThreshold;
        var hourlyHits = window.Count(p => p.Precipitation >= hourlyThreshold);
        var dailySum = window.Sum(p => p.Precipitation ?? 0.0);

        var dailyExceeds = dailySum >= dailyThreshold;
        var hourlyExceeds = hourlyHits > 0;
        var severity = (hourlyExceeds, dailyExceeds) switch
        {
            (true, true) => AlertSeverity.Red,
            (false, true) => AlertSeverity.Orange,
            (true, false) => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var triggerValue = hourlyExceeds ? Math.Round(Math.Max(0.0, peakPrecipitation), 1) : Math.Round(dailySum, 1);
        var threshold = hourlyExceeds ? hourlyThreshold : dailyThreshold;
        var spread = ctx.Current.Ensemble.Points.SpreadAt(wettest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new HeavyRainAlertResult(
            ctx.Location, severity, level.ToConfidence(), triggerValue, threshold,
            PeakValue: null, HoursUntil: hourlyExceeds ? wettest.HoursFrom(ctx.Now) : null, DurationHours: null, Level: level,
            HourlyExceedModels: hourlyHits, DailyExceedModels: dailyExceeds ? 1 : 0);
    }
}
