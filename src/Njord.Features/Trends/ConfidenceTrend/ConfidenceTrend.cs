using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Features.Trends.ConfidenceTrend;

public sealed class ConfidenceTrend(IOptions<ConfidenceTrendConfig> config) : FeatureRule<EnsembleInput>
{
    public const double ChangeThreshold = 0.20;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Trend,
        FeatureCategory.Insight,
        Name: "ConfidenceTrend",
        DefaultEnabled: false,
        new DomainRequirements(Ensemble: EnsembleParams.TemperatureSpread),
        PollDepth: 1);

    public override bool Enabled => config.Value.Enabled;

    protected override EnsembleInput? Project(LocationForecast forecast) =>
        forecast.Ensemble is { } e ? new(e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<EnsembleInput> ctx)
    {
        if (ctx.History.Count < 2)
        {
            return null;
        }

        var latest = ctx.Current.Ensemble;
        var before = ctx.History[1].Ensemble;

        if (AverageSpread(latest, ctx.Now) is not { } currentAverage || AverageSpread(before, ctx.Now) is not { } previousAverage)
        {
            return null;
        }

        var change = previousAverage > 0 ? (currentAverage - previousAverage) / previousAverage : 0.0;
        var direction = change switch
        {
            <= -ChangeThreshold => ConfidenceTrendDirection.Converging,
            >= ChangeThreshold => ConfidenceTrendDirection.Diverging,
            _ => ConfidenceTrendDirection.Stable,
        };
        return new ConfidenceTrendResult(ctx.Location, direction, Math.Round(currentAverage - previousAverage, 2), Math.Round(currentAverage, 2), Math.Round(previousAverage, 2));
    }

    private static double? AverageSpread(DomainForecast<EnsemblePoint> forecast, DateTimeOffset now)
    {
        var values = forecast.NextHours(now)
            .Where(p => p.TemperatureSpread.HasValue)
            .Select(p => p.TemperatureSpread!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
    }
}
