using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.OpenMeteo.Mapping;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.AirQuality;

internal static class AirQualityResponseMappingExtensions
{
    public static DomainForecast<AirQualityPoint>? ToDomainForecast(
        this AirQualityResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        var points = new AirQualityPoint[h.Time.Length];
        for (var i = 0; i < h.Time.Length; i++)
        {
            points[i] = new AirQualityPoint(
                ValidFrom: DateTimeOffset.FromUnixTimeSeconds(h.Time[i]),
                Pm25: At(h.Pm25, i),
                Pm10: At(h.Pm10, i),
                Ozone: At(h.Ozone, i),
                BirchPollen: At(h.BirchPollen, i),
                GrassPollen: At(h.GrassPollen, i),
                Alder: At(h.AlderPollen, i),
                Mugwort: At(h.MugwortPollen, i),
                EuropeanAqi: At(h.EuropeanAqi, i),
                NitrogenDioxide: At(h.NitrogenDioxide, i),
                CarbonMonoxide: At(h.CarbonMonoxide, i),
                SulphurDioxide: At(h.SulphurDioxide, i),
                Dust: At(h.Dust, i),
                Ammonia: At(h.Ammonia, i),
                OlivePollen: At(h.OlivePollen, i),
                UsAqi: At(h.UsAqi, i),
                UvIndex: At(h.UvIndex, i),
                UvIndexClearSky: At(h.UvIndexClearSky, i),
                RagweedPollen: At(h.RagweedPollen, i),
                EuropeanAqiPm25: At(h.EuropeanAqiPm25, i),
                EuropeanAqiPm10: At(h.EuropeanAqiPm10, i),
                EuropeanAqiNitrogenDioxide: At(h.EuropeanAqiNitrogenDioxide, i),
                EuropeanAqiOzone: At(h.EuropeanAqiOzone, i),
                EuropeanAqiSulphurDioxide: At(h.EuropeanAqiSulphurDioxide, i),
                AerosolOpticalDepth: At(h.AerosolOpticalDepth, i));
        }

        return new DomainForecast<AirQualityPoint>(
            DomainKind.AirQuality, modelRun, TimeResolution.Hourly, [.. points]);
    }
}
