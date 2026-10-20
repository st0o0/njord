using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class SolarYieldHandler(IOptions<SolarYieldOptions> options) : IDomainHandler
{
    public const double SystemEfficiency = 0.8;

    public string ConfigKey => "SolarYield";

    public DomainRequirements Requirements { get; } = new(Weather: WeatherParams.DirectRadiation | WeatherParams.DiffuseRadiation);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 24)
            .Where(point => point is { DirectRadiation: not null, DiffuseRadiation: not null })
            .Select(point => (point.ValidFrom, Radiation: point.DirectRadiation!.Value + point.DiffuseRadiation!.Value))
            .ToList();
        if (hours.Count == 0)
        {
            return [];
        }

        var kwhPerSquareMeter = hours.Sum(hour => hour.Radiation) / 1000.0;
        var yield = kwhPerSquareMeter * SystemEfficiency * OrientationFactor(options.Value);
        var peak = hours.MaxBy(hour => hour.Radiation);
        return [new PipelineEvent.SolarYieldComputed(current.Location, Math.Round(yield, 2), peak.ValidFrom.Hour)];
    }

    private static double OrientationFactor(SolarYieldOptions settings)
    {
        var azimuth = 1.0 - 0.25 * Math.Abs(settings.Azimuth - 180) / 180.0;
        var tilt = 1.0 - 0.2 * Math.Abs(settings.Tilt - 35) / 55.0;
        return azimuth * tilt;
    }
}
