using System.Collections.Immutable;
using System.Text.Json;
using Njord.Model;

namespace Njord.OpenMeteo.Mapping;

internal static class OpenMeteoFloodMappings
{
    public static DomainForecast<FloodPoint>? ToFloodForecast(
        this OpenMeteoTimeSeries series, DateTimeOffset modelRun)
    {
        var times = series.Time;
        var builder = ImmutableArray.CreateBuilder<FloodPoint>(times.Count);

        for (var i = 0; i < times.Count; i++)
        {
            var discharge = GetOptionalDouble(series, "river_discharge", i);
            if (discharge is null)
            {
                continue;
            }

            builder.Add(new FloodPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i]),
                discharge.Value,
                DischargeMean: GetOptionalDouble(series, "river_discharge_mean", i),
                DischargeMedian: GetOptionalDouble(series, "river_discharge_median", i),
                DischargeMax: GetOptionalDouble(series, "river_discharge_max", i),
                DischargeMin: GetOptionalDouble(series, "river_discharge_min", i),
                DischargeP25: GetOptionalDouble(series, "river_discharge_p25", i),
                DischargeP75: GetOptionalDouble(series, "river_discharge_p75", i)));
        }

        return builder.Count == 0
            ? null
            : new DomainForecast<FloodPoint>(DomainKind.Flood, modelRun, TimeResolution.Daily, builder.ToImmutable());
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
