using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class ApproachingFrontHandler : IDomainHandler
{
    public const double PressureDropThreshold = 3.0;
    public const double WindShiftThreshold = 45.0;
    public const int WindowHours = 6;

    public string ConfigKey => "ApproachingFront";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather || previous?.Weather is null)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var hours = ForecastQueries.Upcoming(weather, now, 24 + WindowHours);
        var windows = hours
            .Select(start => (Start: start, End: hours.FirstOrDefault(p => p.ValidFrom == start.ValidFrom.AddHours(WindowHours))))
            .Where(pair => pair.End is not null)
            .Select(pair => (pair.Start, End: pair.End!, Drop: (pair.Start.Pressure - pair.End!.Pressure) ?? 0.0))
            .Where(window => window.Drop > PressureDropThreshold && WindShifted(window.Start, window.End))
            .ToList();
        if (windows.Count == 0)
        {
            return [];
        }

        var best = windows.MaxBy(window => window.Drop);
        var estimatedIn = best.Start.ValidFrom - now;
        return
        [
            new PipelineEvent.FrontApproaching(
                current.Location,
                estimatedIn < TimeSpan.Zero ? TimeSpan.Zero : estimatedIn,
                Math.Round(best.Drop, 1),
                Math.Round((best.Start.Temperature - best.End.Temperature) ?? 0.0, 1)),
        ];
    }

    private static bool WindShifted(WeatherPoint start, WeatherPoint end)
    {
        if (start.WindDirection is not { } from || end.WindDirection is not { } to)
        {
            return true;
        }

        var difference = Math.Abs(to - from) % 360.0;
        return Math.Min(difference, 360.0 - difference) >= WindShiftThreshold;
    }
}
