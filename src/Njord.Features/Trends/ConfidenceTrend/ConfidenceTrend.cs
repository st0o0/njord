using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Trends.ConfidenceTrend;

public sealed class ConfidenceTrend(IOptions<ConfidenceTrendConfig> config) : FeatureRule<WeatherInput>
{
    public const double ChangeThreshold = 0.20;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Trend,
        FeatureCategory.Insight,
        Name: "ConfidenceTrend",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.EnsembleStdDev),
        PollDepth: 1);

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        if (ctx.History.Count < 2)
        {
            return null;
        }

        var latest = ctx.Current.Weather;
        var before = ctx.History[1].Weather;

        if (AverageStdDev(latest, ctx.Now) is not { } currentAverage || AverageStdDev(before, ctx.Now) is not { } previousAverage)
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

    private static double? AverageStdDev(DomainForecast<WeatherPoint> forecast, DateTimeOffset now)
    {
        var values = forecast.NextHours(now)
            .Where(p => p.EnsembleStdDev.HasValue)
            .Select(p => p.EnsembleStdDev!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
    }
}
