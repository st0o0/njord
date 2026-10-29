using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Ice;

public sealed class IceAlert(IOptions<IceConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Ice",
        DefaultEnabled: true,
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.Precipitation,
            OptionalForecast: ForecastParams.Rain | ForecastParams.SoilTemperature | ForecastParams.WeatherCode,
            Ensemble: EnsembleParams.PrecipitationSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var icy = ctx.Current.Forecast.NextHoursInclusive(ctx.Now)
            .Select(p => (Point: p, Hit: Classify(p)))
            .Where(x => x.Hit.Severity != AlertSeverity.None)
            .ToList();
        if (icy.Select(x => x.Point).Where(p => p.Temperature is not null).MinBy(p => p.Temperature) is not { Temperature: { } minTemp } coldest)
        {
            return null;
        }

        var severity = icy.Max(x => x.Hit.Severity);
        var threshold = icy.Where(x => x.Hit.Severity == severity).Max(x => x.Hit.Threshold);
        var soilFrozen = icy.Any(x => x.Point.SoilTemperature is <= 0);

        var rounded = Math.Round(minTemp, 1);
        var spread = ctx.Current.Ensemble.Points.SpreadAt(coldest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.PrecipitationSpread);
        return new IceAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, threshold,
            PeakValue: null, HoursUntil: icy[0].Point.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedLow: rounded, RainHours: icy.Count, SoilFrozen: soilFrozen);
    }

    private (AlertSeverity Severity, double Threshold) Classify(ForecastPoint p)
    {
        const double FreezingPoint = 0.0;
        return p.WeatherCode switch
        {
            66 or 67 => (AlertSeverity.Red, FreezingPoint),
            56 or 57 => (AlertSeverity.Orange, FreezingPoint),
            _ when (p.Rain ?? p.Precipitation) > 0 && p.Temperature <= FreezingPoint => (AlertSeverity.Red, FreezingPoint),
            _ when (p.Rain ?? p.Precipitation) > 0 && p.Temperature <= config.Value.Threshold => (AlertSeverity.Yellow, config.Value.Threshold),
            _ => (AlertSeverity.None, config.Value.Threshold),
        };
    }
}
