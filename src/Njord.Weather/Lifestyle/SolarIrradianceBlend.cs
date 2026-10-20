using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Solar;

namespace Njord.Weather.Lifestyle;

public readonly record struct IrradianceHour(DateTimeOffset ValidFrom, double Radiation, bool Observed);

// Irradiance on the panel (W/m2) per hour: hours up to the newest satellite observation come from the satellite GTI,
// later hours from the ensemble GTI. The switch at the seam is hard; satellite data that is too old is ignored.
public static class SolarIrradianceBlend
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(3);

    public static SatelliteRadiationPoint? Latest(DomainForecast<SatelliteRadiationPoint>? satellite, DateTimeOffset now)
    {
        if (satellite is null)
        {
            return null;
        }

        var latest = satellite.Points
            .Where(point => point.GTI is not null && point.ValidFrom <= now)
            .MaxBy(point => point.ValidFrom);
        return latest is not null && now - latest.ValidFrom <= StaleAfter ? latest : null;
    }

    public static IReadOnlyList<IrradianceHour> Hours(LocationForecast forecast, DateTimeOffset now, int hours)
    {
        if (forecast.Weather is not { } weather)
        {
            return [];
        }

        var latest = Latest(forecast.Satellite, now);
        var observed = latest is null
            ? new Dictionary<DateTimeOffset, double>()
            : forecast.Satellite!.Points
                .Where(point => point.GTI is not null && point.ValidFrom <= latest.ValidFrom)
                .ToDictionary(point => point.ValidFrom, point => point.GTI!.Value);

        var result = new List<IrradianceHour>();
        foreach (var point in ForecastQueries.Upcoming(weather, now, hours))
        {
            if (observed.TryGetValue(point.ValidFrom, out var measured))
            {
                result.Add(new IrradianceHour(point.ValidFrom, measured, Observed: true));
            }
            else if (point.GlobalTiltedIrradiance is { } modeled)
            {
                result.Add(new IrradianceHour(point.ValidFrom, modeled, Observed: false));
            }
        }

        return result;
    }
}
