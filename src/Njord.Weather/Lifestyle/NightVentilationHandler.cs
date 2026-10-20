using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Weather.Lifestyle;

public sealed class NightVentilationHandler : IDomainHandler
{
    public const double HotDayPeak = 28.0;
    public const double CoolNightLimit = 20.0;

    public string ConfigKey => "NightVentilation";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var tomorrowStart = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        var tomorrowPoints = weather.Points
            .Where(p => p.ValidFrom >= tomorrowStart && p.ValidFrom < tomorrowStart.AddDays(1))
            .ToList();
        if (tomorrowPoints.Count == 0)
        {
            return [];
        }

        var peak = tomorrowPoints.Max(p => p.Temperature);
        if (peak <= HotDayPeak)
        {
            return [];
        }

        var night = ForecastQueries.Window(weather, AlertClock.HourStart(now), tomorrowStart.AddHours(9))
            .Where(p => p.ValidFrom.UtcDateTime.Hour is >= 18 or < 9)
            .ToList();
        var (start, length) = Scoring.LongestRun([.. night.Select(p => p.Temperature < CoolNightLimit)]);
        if (length == 0)
        {
            return [];
        }

        var window = night.Skip(start).Take(length).ToList();
        var closeAt = window[^1].ValidFrom + weather.Resolution.ToTimeSpan();
        return [new PipelineEvent.NightVentilationAdvised(current.Location, window[0].ValidFrom, closeAt, window.Min(p => p.Temperature), peak)];
    }
}
