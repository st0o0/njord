using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Model;

namespace Njord.Alerts;

internal static class AlertWindow
{
    public const int Hours = 24;

    public static readonly Horizon LastHour = Horizon.Hours(Hours);
    public static readonly Horizon LastDay = Horizon.Days(1);

    public static IEnumerable<Horizon> HourlyHorizons => Enumerable.Range(0, Hours + 1).Select(Horizon.Hours);

    public static IEnumerable<Horizon> DaytimeHours(this ParameterConsensus isDay) =>
        HourlyHorizons.Where(horizon => isDay.MedianAt(horizon) is > 0.5);

    public static IEnumerable<Horizon> NighttimeHours(this ParameterConsensus isDay) =>
        HourlyHorizons.Where(horizon => isDay.MedianAt(horizon) is <= 0.5);

    public static HorizonWindow Next24Hours(this ParameterConsensus series) => series.Within(LastHour);

    public static double? DailyMax(this LocationConsensus consensus, ParameterDef parameter) =>
        consensus.Daily.Find(parameter)?.Within(LastDay).Extreme(ScanDirection.Max)?.Value;

    public static double RoundedAverage(this IEnumerable<double?> agreements)
    {
        var present = agreements.OfType<double>().ToList();
        return present.Count > 0 ? Math.Round(present.Average(), 3) : 0.0;
    }

    public static double RoundedAverage(this IEnumerable<HorizonHit> hits) =>
        hits.Select(hit => hit.Agreement).RoundedAverage();
}
