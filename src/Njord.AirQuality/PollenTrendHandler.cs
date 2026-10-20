using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.AirQuality;

public sealed class PollenTrendHandler : IDomainHandler
{
    public const double ChangeThreshold = 0.10;
    public const double MinimumChange = 1.0;

    private static readonly (string Name, Func<AirQualityPoint, double?> Read)[] PollenTypes =
    [
        ("birch", p => p.BirchPollen),
        ("grass", p => p.GrassPollen),
        ("alder", p => p.Alder),
        ("mugwort", p => p.Mugwort),
    ];

    public string ConfigKey => "PollenTrend";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.AirQuality };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.AirQuality is not { } airQuality || previous?.AirQuality is not { } before)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var currentPoints = ForecastQueries.Upcoming(airQuality, now, 24);
        var previousPoints = ForecastQueries.Upcoming(before, now, 24);

        var trends = new Dictionary<string, string>();
        string? peakType = null;
        DateTimeOffset? peakTime = null;
        var peakValue = 0.0;
        foreach (var (name, read) in PollenTypes)
        {
            var currentValues = currentPoints
                .Select(p => (p.ValidFrom, Value: read(p)))
                .Where(v => v.Value.HasValue)
                .Select(v => (v.ValidFrom, Value: v.Value!.Value))
                .ToList();
            if (currentValues.Count == 0)
            {
                continue;
            }

            var previousValues = previousPoints.Select(read).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (previousValues.Count > 0)
            {
                trends[name] = Classify(currentValues.Average(v => v.Value), previousValues.Average());
            }

            var top = currentValues.MaxBy(v => v.Value);
            if (top.Value > peakValue)
            {
                peakValue = top.Value;
                peakType = name;
                peakTime = top.ValidFrom;
            }
        }

        return trends.Count == 0
            ? []
            : [new PipelineEvent.PollenTrendComputed(current.Location, trends, peakType, peakTime)];
    }

    private static string Classify(double currentAverage, double previousAverage)
    {
        var delta = currentAverage - previousAverage;
        if (Math.Abs(delta) < MinimumChange || Math.Abs(delta) < ChangeThreshold * previousAverage)
        {
            return "stable";
        }

        return delta > 0 ? "rising" : "falling";
    }
}
