using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostAlert(IOptions<FrostConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Frost",
        new DomainRequirements(Forecast: ForecastParams.Temperature, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var thresholds = config.Value.Thresholds.ToSeverity(ScanDirection.Min);
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
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
        var spread = ctx.Current.Ensemble.Points.SpreadAt(coldest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new FrostAlertResult(
            ctx.Location, severity, level.ToConfidence(), low, effectiveThreshold,
            PeakValue: null, HoursUntil: earliest?.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedLow: low, EarliestFrostHours: earliest is null ? null : earliest.HoursFrom(ctx.Now));
    }
}
