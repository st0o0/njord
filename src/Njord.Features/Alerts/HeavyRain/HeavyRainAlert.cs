using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainAlert(IOptions<HeavyRainConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Precipitation | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
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
        var level = ConfidenceThresholds.Default.LevelFor(wettest.EnsembleStdDev);
        return new HeavyRainAlertResult(
            ctx.Location, severity, level.ToConfidence(), triggerValue, threshold,
            PeakValue: null, HoursUntil: hourlyExceeds ? wettest.HoursFrom(ctx.Now) : null, DurationHours: null, Level: level,
            HourlyExceedModels: hourlyHits, DailyExceedModels: dailyExceeds ? 1 : 0);
    }
}
