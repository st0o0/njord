using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.TropicalNight;

public sealed class TropicalNightAlert(IOptions<TropicalNightConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "TropicalNight",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.IsDay, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
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
        var spread = ctx.Current.Ensemble.Points.SpreadAt(coldest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new TropicalNightAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: night[0].HoursFrom(ctx.Now), DurationHours: null, Level: level,
            NightMinimum: rounded, HoursAbove20: hoursAbove);
    }
}
