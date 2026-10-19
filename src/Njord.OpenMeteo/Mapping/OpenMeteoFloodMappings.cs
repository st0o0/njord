using System.Collections.Immutable;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoFloodMappings
{
    public static DomainForecast<FloodPoint>? ToFloodForecast(
        this OpenMeteoResponse response, DateTimeOffset modelRun)
    {
        if (response.Daily is not { } series || series.Time.Length == 0)
        {
            return null;
        }

        var times = series.Time;
        var builder = ImmutableArray.CreateBuilder<FloodPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            var discharge = series.ValueAt( "river_discharge", i);
            if (discharge is null)
            {
                continue;
            }

            builder.Add(new FloodPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                discharge.Value,
                DischargeMean: series.ValueAt( "river_discharge_mean", i),
                DischargeMedian: series.ValueAt( "river_discharge_median", i),
                DischargeMax: series.ValueAt( "river_discharge_max", i),
                DischargeMin: series.ValueAt( "river_discharge_min", i),
                DischargeP25: series.ValueAt( "river_discharge_p25", i),
                DischargeP75: series.ValueAt( "river_discharge_p75", i)));
        }

        return builder.Count == 0
            ? null
            : new DomainForecast<FloodPoint>(DomainKind.Flood, modelRun, TimeResolution.Daily, builder.ToImmutable());
    }
}
