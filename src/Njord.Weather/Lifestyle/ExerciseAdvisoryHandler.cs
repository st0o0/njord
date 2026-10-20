using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class ExerciseAdvisoryHandler : IDomainHandler
{
    public const double SafeScore = 70.0;
    public const double CautionScore = 45.0;

    private const double Pm25Weight = 0.4;
    private const double OzoneWeight = 0.2;
    private const double TemperatureWeight = 0.2;
    private const double HumidityWeight = 0.1;
    private const double UvWeight = 0.1;

    public string ConfigKey => "ExerciseAdvisory";

    public DomainRequirements Requirements { get; } = new(
        Weather: WeatherParam.Temperature | WeatherParam.Humidity,
        AirQuality: AirQualityParam.Pm25 | AirQualityParam.Ozone);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather || current.AirQuality is not { } airQuality)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 12)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(airQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .Select(pair => Components(pair.Point, pair.Air!))
            .OfType<Hour>()
            .ToList();
        if (hours.Count == 0)
        {
            return [];
        }

        var average = hours.Average(h => h.Total);
        var best = hours.MaxBy(h => h.Total)!;
        var level = average switch
        {
            >= SafeScore => "Safe",
            >= CautionScore => "Caution",
            _ => "Avoid",
        };

        var reasons = new List<string>();
        AddReason(reasons, hours.Average(h => h.Pm25), "High particulate matter (PM2.5)");
        AddReason(reasons, hours.Average(h => h.Ozone), "High ozone");
        AddReason(reasons, hours.Average(h => h.Temperature), "Uncomfortable temperature");
        AddReason(reasons, hours.Average(h => h.Humidity), "High humidity");
        AddReason(reasons, hours.Average(h => h.Uv), "Strong UV radiation");

        return [new PipelineEvent.ExerciseAdvised(current.Location, level, [.. reasons], best.ValidFrom)];
    }

    private static void AddReason(List<string> reasons, double componentScore, string text)
    {
        if (componentScore < 0.5)
        {
            reasons.Add(text);
        }
    }

    private static Hour? Components(WeatherPoint weather, AirQualityPoint air)
    {
        if (air is not { Pm25: { } airPm25, Ozone: { } airOzone }
            || weather is not { Temperature: { } airTemperature, Humidity: { } relativeHumidity })
        {
            return null;
        }

        var pm25 = 1.0 - Scoring.Ramp(airPm25, 15.0, 55.0);
        var ozone = 1.0 - Scoring.Ramp(airOzone, 100.0, 180.0);
        var temperature = Scoring.Band(airTemperature, -5.0, 8.0, 22.0, 35.0);
        var humidity = 1.0 - Scoring.Ramp(relativeHumidity, 70.0, 100.0);
        var uv = weather.UvIndex is { } uvIndex ? 1.0 - Scoring.Ramp(uvIndex, 5.0, 11.0) : 1.0;
        var total = 100.0 * (Pm25Weight * pm25 + OzoneWeight * ozone + TemperatureWeight * temperature
            + HumidityWeight * humidity + UvWeight * uv);
        return new Hour(weather.ValidFrom, total, pm25, ozone, temperature, humidity, uv);
    }

    private sealed record Hour(
        DateTimeOffset ValidFrom,
        double Total,
        double Pm25,
        double Ozone,
        double Temperature,
        double Humidity,
        double Uv);
}
