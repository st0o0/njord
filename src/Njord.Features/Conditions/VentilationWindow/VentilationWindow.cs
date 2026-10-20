using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Shared;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.VentilationWindow;

public sealed class VentilationWindow(IOptions<VentilationWindowConfig> config) : FeatureRule<WeatherAqInput>
{
    public const double MaxPm25 = 15.0;
    public const double MaxPollen = 20.0;
    public const double MinTemperature = 15.0;
    public const double MaxTemperature = 28.0;
    public const double MaxPrecipitation = 0.1;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(
        Weather: WeatherParams.Temperature | WeatherParams.Precipitation,
        AirQuality: AirQualityParams.Pm25,
        OptionalAirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
            | AirQualityParams.Alder | AirQualityParams.Mugwort));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherAqInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherAqInput> ctx)
    {
        var hours = ctx.Current.Weather.NextHours(ctx.Now)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(ctx.Current.AirQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .ToList();
        if (hours.Count == 0)
        {
            return null;
        }

        var open = hours.Select(pair => IsOpenWindowHour(pair.Point, pair.Air!)).ToList();
        var (start, length) = Scoring.LongestRun(open);
        if (length == 0)
        {
            return null;
        }

        var first = hours[start].Point.ValidFrom;
        var end = hours[start + length - 1].Point.ValidFrom + ctx.Current.Weather.Resolution.ToTimeSpan();
        var reason = $"Clean air and mild dry weather for {length} h";
        return new VentilationWindowResult(ctx.Location, "open", FeatureData.Of(
            ("best_start", FeatureData.Time(first)),
            ("best_end", FeatureData.Time(end)),
            ("reason", reason)));
    }

    private static bool IsOpenWindowHour(WeatherPoint weather, AirQualityPoint air)
    {
        var pollen = Math.Max(
            Math.Max(air.BirchPollen ?? 0, air.GrassPollen ?? 0),
            Math.Max(air.Alder ?? 0, air.Mugwort ?? 0));
        return air.Pm25 < MaxPm25
            && pollen < MaxPollen
            && weather.Temperature is >= MinTemperature and <= MaxTemperature
            && weather.Precipitation <= MaxPrecipitation;
    }
}
