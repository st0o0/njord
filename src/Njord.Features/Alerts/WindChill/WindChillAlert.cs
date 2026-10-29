using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.WindChill;

public sealed class WindChillAlert(IOptions<WindChillConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "WindChill",
        new DomainRequirements(Forecast: ForecastParams.ApparentTemperature, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var thresholds = config.Value.Thresholds.ToSeverity(ScanDirection.Min);
        if (ctx.Current.Forecast.NextHoursInclusive(ctx.Now).LowestBy(p => p.ApparentTemperature) is not { ApparentTemperature: { } low } coldest)
        {
            return null;
        }

        var windFactor = Math.Round((coldest.Temperature ?? low) - low, 1);
        var (severity, effectiveThreshold) = thresholds.Classify(low);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var exposureRisk = severity switch
        {
            AlertSeverity.Red => ExposureRisk.Frostbite10Minutes,
            AlertSeverity.Orange => ExposureRisk.Frostbite30Minutes,
            _ => (ExposureRisk?)null,
        };

        var rounded = Math.Round(low, 1);
        var spread = ctx.Current.Ensemble.Points.SpreadAt(coldest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new WindChillAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: coldest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            FeltTemperature: rounded, WindFactor: windFactor, ExposureRisk: exposureRisk);
    }
}
