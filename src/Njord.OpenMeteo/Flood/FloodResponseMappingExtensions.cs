using System.Collections.Immutable;
using System.Text.Json;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.OpenMeteo.Mapping;
using static Njord.OpenMeteo.Mapping.SeriesAccess;

namespace Njord.OpenMeteo.Flood;

internal static class FloodResponseMappingExtensions
{
    public static DomainForecast<FloodPoint>? ToDomainForecast(
        this FloodResponse response, DateTimeOffset modelRun)
    {
        if (response.Daily is not { } d || d.Time.Length == 0)
        {
            return null;
        }

        var times = d.Time;
        var members = d.MemberSeries();
        var builder = ImmutableArray.CreateBuilder<FloodPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            if (!AnyAt(i, d.Discharge, d.DischargeMean, d.DischargeMedian, d.DischargeMax,
                    d.DischargeMin, d.DischargeP25, d.DischargeP75)
                && !members.Any(m => At(m, i) is not null))
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
                DischargeP75: At(d.DischargeP75, i),
                EnsembleMembers: MembersAt(members, i)));
        }

        return builder.Count == 0
            ? null
            : new DomainForecast<FloodPoint>(DomainKind.Flood, modelRun, TimeResolution.Daily, builder.ToImmutable());
    }

    private const string MemberPrefix = "river_discharge_member";

    private static double?[][] MemberSeries(this FloodDailyData data) =>
        data.Extra is null
            ? []
            : [.. data.Extra
                .Where(e => e.Key.StartsWith(MemberPrefix, StringComparison.Ordinal) && e.Value.ValueKind == JsonValueKind.Array)
                .OrderBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => e.Value.EnumerateArray()
                    .Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null)
                    .ToArray())];

    private static IReadOnlyList<double>? MembersAt(double?[][] members, int index)
    {
        if (members.Length == 0)
        {
            return null;
        }

        return [.. members.Select(m => At(m, index)).OfType<double>()];
    }
}
