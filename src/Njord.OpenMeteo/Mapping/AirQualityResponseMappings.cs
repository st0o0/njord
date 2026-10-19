using System.Collections.Immutable;
using Njord.Model;

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
            var pm25 = At(h.Pm25, i);
            var pm10 = At(h.Pm10, i);
            var ozone = At(h.Ozone, i);

            if (pm25 is not null || pm10 is not null || ozone is not null)
            {
                lastValued = i;
            }

            builder.Add(new AirQualityPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                pm25 ?? 0,
                pm10 ?? 0,
                ozone ?? 0,
                BirchPollen: OptAt(h.BirchPollen, i),
                GrassPollen: OptAt(h.GrassPollen, i),
                Alder: OptAt(h.AlderPollen, i),
                Mugwort: OptAt(h.MugwortPollen, i),
                EuropeanAqi: OptAt(h.EuropeanAqi, i),
                NitrogenDioxide: OptAt(h.NitrogenDioxide, i)));
        }

        if (lastValued < 0)
        {
            return null;
        }

        return new DomainForecast<AirQualityPoint>(
            DomainKind.AirQuality, modelRun, TimeResolution.Hourly,
            builder.Take(lastValued + 1).ToImmutableArray());
    }

    private static double? At(double?[] array, int index) =>
        index < array.Length ? array[index] : null;

    private static double? OptAt(double?[]? array, int index) =>
        array is not null && index < array.Length ? array[index] : null;
}
