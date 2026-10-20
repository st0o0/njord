using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Mapping;

internal static class AirQualityResponseMappings
{
    public static DomainForecast<AirQualityPoint>? ToDomainForecast(
        this AirQualityResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not var (times, pm25, pm10, ozone, nitrogenDioxide, europeanAqi, birchPollen, grassPollen, alderPollen, mugwortPollen, _, carbonMonoxide, sulphurDioxide, dust, ammonia, olivePollen, usAqi, uvIndex, uvIndexClearSky) || times.Length == 0)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<AirQualityPoint>(times.Length);
        var lastValued = -1;

        for (var i = 0; i < times.Length; i++)
        {
            if (AnyAt(i, pm25, pm10, ozone, nitrogenDioxide, europeanAqi, birchPollen,
                    grassPollen, alderPollen, mugwortPollen, carbonMonoxide, sulphurDioxide, dust,
                    ammonia, olivePollen, usAqi, uvIndex, uvIndexClearSky))
            {
                lastValued = i;
            }

            builder.Add(new AirQualityPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                Pm25: At(pm25, i),
                Pm10: At(pm10, i),
                Ozone: At(ozone, i),
                BirchPollen: At(birchPollen, i),
                GrassPollen: At(grassPollen, i),
                Alder: At(alderPollen, i),
                Mugwort: At(mugwortPollen, i),
                EuropeanAqi: At(europeanAqi, i),
                NitrogenDioxide: At(nitrogenDioxide, i),
                CarbonMonoxide: At(carbonMonoxide, i),
                SulphurDioxide: At(sulphurDioxide, i),
                Dust: At(dust, i),
                Ammonia: At(ammonia, i),
                OlivePollen: At(olivePollen, i),
                UsAqi: At(usAqi, i),
                UvIndex: At(uvIndex, i),
                UvIndexClearSky: At(uvIndexClearSky, i)));
        }

        if (lastValued < 0)
        {
            return null;
        }

        return new DomainForecast<AirQualityPoint>(
            DomainKind.AirQuality, modelRun, TimeResolution.Hourly,
            builder.Take(lastValued + 1).ToImmutableArray());
    }
}
