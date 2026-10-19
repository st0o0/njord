using System.Collections.Immutable;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class FloodResponseMappings
{
    public static DomainForecast<FloodPoint>? ToDomainForecast(
        this FloodResponse response, DateTimeOffset modelRun)
    {
        if (response.Daily is not { } d || d.Time.Length == 0)
        {
            return null;
        }

        var times = d.Time;
        var builder = ImmutableArray.CreateBuilder<FloodPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            var discharge = At(d.Discharge, i);
            if (discharge is null)
            {
                continue;
            }

            builder.Add(new FloodPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                discharge.Value,
                DischargeMean: OptAt(d.DischargeMean, i),
                DischargeMedian: OptAt(d.DischargeMedian, i),
                DischargeMax: OptAt(d.DischargeMax, i),
                DischargeMin: OptAt(d.DischargeMin, i),
                DischargeP25: OptAt(d.DischargeP25, i),
                DischargeP75: OptAt(d.DischargeP75, i)));
        }

        return builder.Count == 0
            ? null
            : new DomainForecast<FloodPoint>(DomainKind.Flood, modelRun, TimeResolution.Daily, builder.ToImmutable());
    }

    private static double? At(double?[] array, int index) =>
        index < array.Length ? array[index] : null;

    private static double? OptAt(double?[]? array, int index) =>
        array is not null && index < array.Length ? array[index] : null;
}
