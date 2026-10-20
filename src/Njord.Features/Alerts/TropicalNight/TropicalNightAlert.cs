using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.TropicalNight;

public sealed class TropicalNightAlert(IOptions<TropicalNightConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.IsDay | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        var night = ctx.Current.Weather.NextHoursInclusive(ctx.Now).Where(p => p is { IsDay: <= 0.5, Temperature: not null }).ToList();
        if (night.Count == 0)
        {
            return null;
        }

        if (night.MinBy(p => p.Temperature) is not { Temperature: { } nightLow } coldest)
        {
            return null;
        }

        var hoursAbove = night.Count(p => p.Temperature is { } temp && thresholds.CrossesAdvisory(temp, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(nightLow, strict: true);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(nightLow, 1);
        var level = ConfidenceThresholds.Default.LevelFor(coldest.EnsembleStdDev);
        return new TropicalNightAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: night[0].HoursFrom(ctx.Now), DurationHours: null, Level: level,
            NightMinimum: rounded, HoursAbove20: hoursAbove);
    }
}
