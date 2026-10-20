using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Advisories;

// Hint-only: does not know indoor temperature. Best used as a notification trigger, not an automation input.
public sealed class NightVentilationHandler : IDomainHandler
{
    public const double HotDayPeak = 28.0;
    public const double CoolNightLimit = 20.0;

    public string Name => "NightVentilation";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Temperature));

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

        if (tomorrowPoints.Max(p => p.Temperature) is not { } peak || peak <= HotDayPeak)
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
        if (window.Min(p => p.Temperature) is not { } coolest)
        {
            return [];
        }

        var closeAt = window[^1].ValidFrom + weather.Resolution.ToTimeSpan();
        return [new ConditionResult(current.Location, this.WireKey(), "open", FeatureData.Of(
            ("open_at", FeatureData.Time(window[0].ValidFrom)),
            ("close_at", FeatureData.Time(closeAt)),
            ("low_temp", FeatureData.Number(coolest)),
            ("tomorrow_peak", FeatureData.Number(peak))))];
    }
}
