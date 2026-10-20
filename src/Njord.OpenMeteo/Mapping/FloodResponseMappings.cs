using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.Flood;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

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
            if (!AnyAt(i, d.Discharge, d.DischargeMean, d.DischargeMedian, d.DischargeMax,
                    d.DischargeMin, d.DischargeP25, d.DischargeP75))
            {
                continue;
            }

            builder.Add(new FloodPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                Discharge: At(d.Discharge, i),
                DischargeMean: At(d.DischargeMean, i),
                DischargeMedian: At(d.DischargeMedian, i),
                DischargeMax: At(d.DischargeMax, i),
                DischargeMin: At(d.DischargeMin, i),
                DischargeP25: At(d.DischargeP25, i),
                DischargeP75: At(d.DischargeP75, i)));
        }

        return builder.Count == 0
            ? null
            : new DomainForecast<FloodPoint>(DomainKind.Flood, modelRun, TimeResolution.Daily, builder.ToImmutable());
    }
}
