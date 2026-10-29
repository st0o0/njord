using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Thunderstorm;

public sealed class ThunderstormAlert(IOptions<ThunderstormConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Thunderstorm",
        DefaultEnabled: true,
        new DomainRequirements(
            Forecast: ForecastParams.Cape | ForecastParams.Precipitation | ForecastParams.WindGusts,
            OptionalForecast: ForecastParams.ConvectiveInhibition,
            Ensemble: EnsembleParams.WindSpeedSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var cfg = config.Value;
        var capes = cfg.EffectiveCapeThresholds;
        var evaluable = ctx.Current.Forecast.NextHoursInclusive(ctx.Now).Where(p => p.Cape is not null && p.WindGusts is not null).ToList();
        if (evaluable.Count == 0)
        {
            return null;
        }

        var stormHours = evaluable.Where(p => p.Cape >= capes[0]).ToList();
        if (stormHours.Count == 0)
        {
            return null;
        }

        var severity = AlertSeverity.None;
        ForecastPoint? peak = null;
        foreach (var p in stormHours)
        {
            var hourSeverity = HourSeverity(p, cfg, capes);
            if (peak is null || hourSeverity > severity || (hourSeverity == severity && p.Cape > peak.Cape))
            {
                severity = hourSeverity;
                peak = p;
            }
        }

        if (peak?.ConvectiveInhibition > cfg.CinThreshold)
        {
            severity = Downgrade(severity);
        }

        var maxCape = evaluable.Max(p => p.Cape) ?? 0.0;
        var spread = peak is not null ? ctx.Current.Ensemble.Points.SpreadAt(peak.ValidFrom) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.WindSpeedSpread);
        return new ThunderstormAlertResult(
            ctx.Location, severity, level.ToConfidence(), Math.Round(maxCape, 0), capes[0],
            PeakValue: null, HoursUntil: stormHours[0].HoursFrom(ctx.Now), DurationHours: stormHours.Count, Level: level);
    }

    private static AlertSeverity HourSeverity(ForecastPoint p, ThunderstormConfig cfg, double[] capes)
    {
        var gusts = p.WindGusts ?? 0.0;
        var precip = p.Precipitation ?? 0.0;
        var cape = p.Cape ?? 0.0;
        if (cape >= capes[2] && (gusts >= cfg.GustThreshold * 1.25 || precip >= cfg.PrecipThreshold * 2))
        {
            return AlertSeverity.Red;
        }

        if (cape >= capes[1] && (gusts >= cfg.GustThreshold || precip >= cfg.PrecipThreshold))
        {
            return AlertSeverity.Orange;
        }

        return AlertSeverity.Yellow;
    }

    private static AlertSeverity Downgrade(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Red => AlertSeverity.Orange,
        AlertSeverity.Orange => AlertSeverity.Yellow,
        _ => severity,
    };
}
