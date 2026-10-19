using System.Collections.Immutable;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoAirQualityMappings
{
    public static DomainForecast<AirQualityPoint>? ToAirQualityForecast(
        this OpenMeteoResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } series || series.Time.Length == 0)
        {
            return null;
        }

        var times = series.Time;

        var builder = ImmutableArray.CreateBuilder<AirQualityPoint>(times.Length);
        var lastValued = -1;

        for (var i = 0; i < times.Length; i++)
        {
            var pm25 = series.ValueAt( "pm2_5", i);
            var pm10 = series.ValueAt( "pm10", i);
            var ozone = series.ValueAt( "ozone", i);

            if (pm25 is not null || pm10 is not null || ozone is not null)
            {
                lastValued = i;
            }

            builder.Add(new AirQualityPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                pm25 ?? 0,
                pm10 ?? 0,
                ozone ?? 0,
                BirchPollen: series.ValueAt( "birch_pollen", i),
                GrassPollen: series.ValueAt( "grass_pollen", i),
                Alder: series.ValueAt( "alder_pollen", i),
                Mugwort: series.ValueAt( "mugwort_pollen", i),
                EuropeanAqi: series.ValueAt( "european_aqi", i),
                NitrogenDioxide: series.ValueAt( "nitrogen_dioxide", i)));
        }

        if (lastValued < 0)
        {
            return null;
        }

        return new DomainForecast<AirQualityPoint>(
            DomainKind.AirQuality,
            modelRun,
            TimeResolution.Hourly,
            builder.Take(lastValued + 1).ToImmutableArray());
    }
}
