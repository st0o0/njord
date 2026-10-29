using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.NightVentilation;

public sealed class NightVentilation(IOptions<NightVentilationConfig> config) : FeatureRule<ForecastInput>
{
    public const double HotDayPeak = 28.0;
    public const double CoolNightLimit = 20.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "NightVentilation",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Temperature));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var weather = ctx.Current.Forecast;
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
