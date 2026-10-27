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
            OptionalForecast: ForecastParams.ConvectiveInhibition | ForecastParams.LiftedIndex,
            Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var evaluable = ctx.Current.Forecast.NextHoursInclusive(ctx.Now).Where(p => p.Cape is not null && p.WindGusts is not null).ToList();
        if (evaluable.Count == 0)
        {
            return null;
        }

        var meetAll = evaluable
            .Where(p => p.Cape > config.Value.CapeThreshold
                && p.Precipitation > config.Value.PrecipThreshold
                && p.WindGusts > config.Value.GustThreshold)
            .ToList();
        var share = (double)meetAll.Count / evaluable.Count;
        var severity = share switch
        {
            > 0.75 => AlertSeverity.Red,
            >= 0.5 => AlertSeverity.Orange,
            > 0 => AlertSeverity.Yellow,
            _ => AlertSeverity.None,
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var peak = (meetAll.Count > 0 ? meetAll : evaluable).HighestBy(p => p.Cape);
        severity = ApplyStabilityModifier(severity, peak, config.Value);

        var maxCape = evaluable.Max(p => p.Cape) ?? 0.0;
        var spread = peak is not null ? ctx.Current.Ensemble.Points.SpreadAt(peak.ValidFrom) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new ThunderstormAlertResult(
            ctx.Location, severity, level.ToConfidence(), Math.Round(maxCape, 0), config.Value.CapeThreshold,
            PeakValue: null, HoursUntil: meetAll.Count > 0 ? meetAll[0].HoursFrom(ctx.Now) : null, DurationHours: null, Level: level);
    }

    private static AlertSeverity ApplyStabilityModifier(AlertSeverity severity, ForecastPoint? peak, ThunderstormConfig cfg)
    {
        if (peak is not { ConvectiveInhibition: { } cin, LiftedIndex: { } li })
        {
            return severity;
        }

        if (cin < cfg.CinThreshold && li < cfg.LiftedIndexThreshold)
        {
            return severity switch
            {
                AlertSeverity.Yellow => AlertSeverity.Orange,
                AlertSeverity.Orange => AlertSeverity.Red,
                _ => severity,
            };
        }

        if (cin > 0)
        {
            return severity switch
            {
                AlertSeverity.Red => AlertSeverity.Orange,
                AlertSeverity.Orange => AlertSeverity.Yellow,
                _ => severity,
            };
        }

        return severity;
    }
}
