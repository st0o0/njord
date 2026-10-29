using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainAlert(IOptions<HeavyRainConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "HeavyRain",
        DefaultEnabled: true,
        new DomainRequirements(Forecast: ForecastParams.Precipitation, Ensemble: EnsembleParams.PrecipitationSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
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
        var level = ConfidenceThresholds.Default.LevelFor(spread?.PrecipitationSpread);
        return new HeavyRainAlertResult(
            ctx.Location, severity, level.ToConfidence(), triggerValue, threshold,
            PeakValue: null, HoursUntil: hourlyExceeds ? wettest.HoursFrom(ctx.Now) : null, DurationHours: null, Level: level,
            HourlyExceedModels: hourlyHits, DailyExceedModels: dailyExceeds ? 1 : 0);
    }
}
