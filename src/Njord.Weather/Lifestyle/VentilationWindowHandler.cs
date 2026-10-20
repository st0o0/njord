using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class VentilationWindowHandler : IDomainHandler
{
    public const double MaxPm25 = 15.0;
    public const double MaxPollen = 20.0;
    public const double MinTemperature = 15.0;
    public const double MaxTemperature = 28.0;
    public const double MaxPrecipitation = 0.1;

    public string Name => "Ventilation";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(
        Weather: WeatherParams.Temperature | WeatherParams.Precipitation,
        AirQuality: AirQualityParams.Pm25,
        OptionalAirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
            | AirQualityParams.Alder | AirQualityParams.Mugwort));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather || current.AirQuality is not { } airQuality)
        {
            return [];
        }

        var hours = ForecastWindow.NextHours(weather, time.GetUtcNow())
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(airQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .ToList();
        if (hours.Count == 0)
        {
            return [];
        }

        var open = hours.Select(pair => IsOpenWindowHour(pair.Point, pair.Air!)).ToList();
        var (start, length) = Scoring.LongestRun(open);
        if (length == 0)
        {
            return [];
        }

        var first = hours[start].Point.ValidFrom;
        var end = hours[start + length - 1].Point.ValidFrom + weather.Resolution.ToTimeSpan();
        var reason = $"Clean air and mild dry weather for {length} h";
        return [new ConditionResult(current.Location, this.WireKey(), "open", FeatureData.Of(
            ("best_start", FeatureData.Time(first)),
            ("best_end", FeatureData.Time(end)),
            ("reason", reason)))];
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
