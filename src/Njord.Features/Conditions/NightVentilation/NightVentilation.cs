using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Shared;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.NightVentilation;

public sealed class NightVentilation(IOptions<NightVentilationConfig> config) : FeatureRule<WeatherInput>
{
    public const double HotDayPeak = 28.0;
    public const double CoolNightLimit = 20.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Temperature));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var weather = ctx.Current.Weather;
        var tomorrowStart = new DateTimeOffset(ctx.Now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        var tomorrowPoints = weather.Points
            .Where(p => p.ValidFrom >= tomorrowStart && p.ValidFrom < tomorrowStart.AddDays(1))
            .ToList();
        if (tomorrowPoints.Count == 0)
        {
            return null;
        }

        if (tomorrowPoints.Max(p => p.Temperature) is not { } peak || peak <= HotDayPeak)
        {
            return null;
        }

        var night = ForecastQueries.Window(weather, ctx.Now.HourStart(), tomorrowStart.AddHours(9))
            .Where(p => p.ValidFrom.UtcDateTime.Hour is >= 18 or < 9)
            .ToList();
        var (start, length) = Scoring.LongestRun([.. night.Select(p => p.Temperature < CoolNightLimit)]);
        if (length == 0)
        {
            return null;
        }

        var window = night.Skip(start).Take(length).ToList();
        if (window.Min(p => p.Temperature) is not { } coolest)
        {
            return null;
        }

        var closeAt = window[^1].ValidFrom + weather.Resolution.ToTimeSpan();
        return new NightVentilationResult(ctx.Location, window[0].ValidFrom, closeAt, coolest, peak);
    }
}
