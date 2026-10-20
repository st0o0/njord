using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostAlert(IOptions<FrostConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Min);
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        if (window.LowestBy(p => p.Temperature) is not { Temperature: { } lowest } coldest)
        {
            return null;
        }

        var earliest = window.FirstOrDefault(p => p.Temperature is { } temp && thresholds.CrossesAdvisory(temp));
        var (severity, effectiveThreshold) = thresholds.Classify(lowest);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var low = Math.Round(lowest, 1);
        var level = ConfidenceThresholds.Default.LevelFor(coldest.EnsembleStdDev);
        return new FrostAlertResult(
            ctx.Location, severity, level.ToConfidence(), low, effectiveThreshold,
            PeakValue: null, HoursUntil: earliest?.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedLow: low, EarliestFrostHours: earliest is null ? null : earliest.HoursFrom(ctx.Now));
    }
}
