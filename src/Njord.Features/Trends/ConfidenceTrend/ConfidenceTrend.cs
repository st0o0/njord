using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Trends;
using Njord.Messages.Stream;

namespace Njord.Features.Trends.ConfidenceTrend;

public sealed class ConfidenceTrend(IOptions<ConfidenceTrendConfig> config) : FeatureRule<EnsembleInput>
{
    private const double ChangeThreshold = 0.20;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Trend,
        FeatureCategory.Insight,
        Name: "ConfidenceTrend",
        new DomainRequirements(Ensemble: EnsembleParams.TemperatureSpread),
        PollDepth: 1,
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor));

    public override bool Enabled => config.Value.Enabled;

    protected override EnsembleInput? Project(LocationForecast forecast)
        => forecast.Ensemble is { } e ? new EnsembleInput(e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<EnsembleInput> ctx)
    {
        var latest = ctx.Current.Ensemble;
        if (AverageSpread(latest, ctx.Now) is not { } currentAverage)
        {
            return null;
        }

        if (ctx.History.Count < 2 || AverageSpread(ctx.History[1].Ensemble, ctx.Now) is not { } previousAverage)
        {
            return new ConfidenceTrendResult(ctx.Location, ConfidenceTrendDirection.Stable, 0.0,
                Math.Round(currentAverage, 2), Math.Round(currentAverage, 2));
        }

        var change = previousAverage > 0 ? (currentAverage - previousAverage) / previousAverage : 0.0;
        var direction = change switch
        {
            <= -ChangeThreshold => ConfidenceTrendDirection.Converging,
            >= ChangeThreshold => ConfidenceTrendDirection.Diverging,
            _ => ConfidenceTrendDirection.Stable,
        };
        return new ConfidenceTrendResult(ctx.Location, direction, Math.Round(currentAverage - previousAverage, 2),
            Math.Round(currentAverage, 2), Math.Round(previousAverage, 2));
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
