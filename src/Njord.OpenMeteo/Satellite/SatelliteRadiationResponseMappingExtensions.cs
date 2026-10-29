using Njord.Core;
using Njord.Core.Forecast;
using Njord.Core.Solar;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Satellite;

internal static class SatelliteRadiationResponseMappingExtensions
{
    public static DomainForecast<SatelliteRadiationPoint>? ToDomainForecast(
        this SatelliteRadiationResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        var points = new SatelliteRadiationPoint[h.Time.Length];
        for (var i = 0; i < h.Time.Length; i++)
        {
            points[i] = new SatelliteRadiationPoint(
                DateTimeOffset.FromUnixTimeSeconds(h.Time[i]),
                GHI: At(h.ShortwaveRadiation, i),
                DirectRadiation: At(h.DirectRadiation, i),
                DiffuseRadiation: At(h.DiffuseRadiation, i),
                DNI: At(h.DirectNormalIrradiance, i),
                GTI: At(h.GlobalTiltedIrradiance, i));
        }

        return new DomainForecast<SatelliteRadiationPoint>(
            DomainKind.Satellite, modelRun, TimeResolution.Hourly, [.. points]);
    }
}
