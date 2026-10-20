using System.Collections.Immutable;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class MarineResponseMappings
{
    public static DomainForecast<MarinePoint>? ToDomainForecast(
        this MarineResponse response, DateTimeOffset modelRun)
    {
        if (response.Hourly is not { } h || h.Time.Length == 0)
        {
            return null;
        }

        var times = h.Time;
        var builder = ImmutableArray.CreateBuilder<MarinePoint>(times.Length);
        var lastValued = -1;

        for (var i = 0; i < times.Length; i++)
        {
            var height = At(h.WaveHeight, i);
            var period = At(h.WavePeriod, i);
            var direction = At(h.WaveDirection, i);

            if (height is not null || period is not null || direction is not null)
            {
                lastValued = i;
            }

            builder.Add(new MarinePoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                height ?? 0,
                period ?? 0,
                direction ?? 0,
                WaterTemperature: OptAt(h.SeaSurfaceTemperature, i)));
        }

        if (lastValued < 0)
        {
            return null;
        }

        return new DomainForecast<MarinePoint>(
            DomainKind.Marine, modelRun, TimeResolution.Hourly,
            builder.Take(lastValued + 1).ToImmutableArray());
    }

    private static double? At(double?[] array, int index) =>
        index < array.Length ? array[index] : null;

    private static double? OptAt(double?[]? array, int index) =>
        array is not null && index < array.Length ? array[index] : null;
}
