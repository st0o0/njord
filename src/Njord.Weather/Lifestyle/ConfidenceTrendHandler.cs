using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class ConfidenceTrendHandler : IDomainHandler
{
    public const double ChangeThreshold = 0.20;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.ConfidenceTrend,
        FeatureCategory.Insight,
        "Trend of the ensemble forecast confidence",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.EnsembleStdDev));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } latest || previous?.Weather is not { } before)
        {
            return [];
        }

        var now = time.GetUtcNow();
        if (AverageStdDev(latest, now) is not { } currentAverage || AverageStdDev(before, now) is not { } previousAverage)
        {
            return [];
        }

        var change = previousAverage > 0 ? (currentAverage - previousAverage) / previousAverage : 0.0;
        var direction = change switch
        {
            <= -ChangeThreshold => "Converging",
            >= ChangeThreshold => "Diverging",
            _ => "Stable",
        };
        return [new PipelineEvent.ConfidenceTrend(current.Location, direction, Math.Round(currentAverage, 2), Math.Round(previousAverage, 2))];
    }

    private static double? AverageStdDev(DomainForecast<WeatherPoint> forecast, DateTimeOffset now)
    {
        var values = ForecastWindow.NextHours(forecast, now)
            .Where(p => p.EnsembleStdDev.HasValue)
            .Select(p => p.EnsembleStdDev!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
    }
}
