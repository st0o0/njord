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
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        var times = h.Time;
        var builder = ImmutableArray.CreateBuilder<AirQualityPoint>(times.Length);
        var lastValued = -1;

        for (var i = 0; i < times.Length; i++)
        {
            if (AnyAt(i, h.Pm25, h.Pm10, h.Ozone, h.NitrogenDioxide, h.EuropeanAqi, h.BirchPollen,
                    h.GrassPollen, h.AlderPollen, h.MugwortPollen, h.CarbonMonoxide, h.SulphurDioxide, h.Dust,
                    h.Ammonia, h.OlivePollen, h.UsAqi, h.UvIndex, h.UvIndexClearSky))
            {
                lastValued = i;
            }

            builder.Add(new AirQualityPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
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
                UvIndexClearSky: At(h.UvIndexClearSky, i)));
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
