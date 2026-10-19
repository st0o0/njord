using System.Collections.Immutable;
using System.Text.Json;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoAirQualityMappings
{
    public static DomainForecast<AirQualityPoint>? ToAirQualityForecast(
        this OpenMeteoTimeSeries series, DateTimeOffset modelRun)
    {
        var times = series.Time;
        if (times.Count == 0)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<AirQualityPoint>(times.Count);
        var lastValued = -1;

        for (var i = 0; i < times.Count; i++)
        {
            var pm25 = GetOptionalDouble(series, "pm2_5", i);
            var pm10 = GetOptionalDouble(series, "pm10", i);
            var ozone = GetOptionalDouble(series, "ozone", i);

            if (pm25 is not null || pm10 is not null || ozone is not null)
            {
                lastValued = i;
            }

            builder.Add(new AirQualityPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                pm25 ?? 0,
                pm10 ?? 0,
                ozone ?? 0,
                BirchPollen: GetOptionalDouble(series, "birch_pollen", i),
                GrassPollen: GetOptionalDouble(series, "grass_pollen", i),
                Alder: GetOptionalDouble(series, "alder_pollen", i),
                Mugwort: GetOptionalDouble(series, "mugwort_pollen", i),
                EuropeanAqi: GetOptionalDouble(series, "european_aqi", i),
                NitrogenDioxide: GetOptionalDouble(series, "nitrogen_dioxide", i)));
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

    private static double? GetOptionalDouble(OpenMeteoTimeSeries series, string paramName, int index)
    {
        if (!series.Variables.TryGetValue(paramName, out var element)
            || element.ValueKind != JsonValueKind.Array
            || index >= element.GetArrayLength())
        {
            return null;
        }

        var value = element[index];
        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }
}
