using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Weather;

namespace Njord.Indices;

public static class DaySlicer
{
    private const int LastDayOffset = 2;
    private const double SecondsPerHour = 3600.0;

    // Parameters whose pessimistic value is the lower confidence bound; all others use the upper bound.
    private static readonly ParameterDef[] HigherIsBetter =
        [ParameterRegistry.Temperature2m, ParameterRegistry.ShortwaveRadiation, ParameterRegistry.Et0FaoEvapotranspiration];

    private static readonly ParameterDef[] BoundParameters =
    [
        ParameterRegistry.Temperature2m, ParameterRegistry.RelativeHumidity2m, ParameterRegistry.WindSpeed10m,
        ParameterRegistry.PrecipitationProbability, ParameterRegistry.CloudCover, ParameterRegistry.ShortwaveRadiation,
        ParameterRegistry.Et0FaoEvapotranspiration,
    ];

    private static readonly ParameterDef[] AgreementParameters = BoundParameters[..^1];

    public static IReadOnlyList<DaySlice> Slice(LocationConsensus consensus, DateTimeOffset now)
    {
        var hourly = consensus.Hourly;
        if (hourly.CutoffHour < 0)
        {
            return [];
        }

        var isDay = hourly.Find(ParameterRegistry.IsDay);
        var slices = new List<DaySlice>();
        foreach (var (dayOffset, hours) in HoursByDay(hourly.CutoffHour, now))
        {
            var dayHours = hours.Where(h => isDay is null || isDay.MedianAt(Horizon.Hours(h)) is > 0.5).ToList();
            var nightHours = hours.Except(dayHours).ToList();
            var means = new PeriodMeans(
                MedianMeans(hourly.Parameters, dayHours),
                MedianMeans(hourly.Parameters, nightHours),
                MedianMeans(hourly.Parameters, hours));

            slices.Add(new DaySlice(dayOffset, means, dayHours.Count, nightHours.Count,
                SunshinePercent(hourly, isDay, hours), Bounds(hourly, dayHours, nightHours, hours)));
        }

        return slices;
    }

    private static SortedDictionary<int, List<int>> HoursByDay(int cutoffHour, DateTimeOffset now)
    {
        var todayMidnight = now.UtcDateTime.Date;
        var hoursByDay = new SortedDictionary<int, List<int>>();
        for (var h = 0; h <= cutoffHour; h++)
        {
            var dayOffset = (int)Math.Floor((now.AddHours(h).UtcDateTime - todayMidnight).TotalDays);
            if (dayOffset > LastDayOffset)
            {
                break;
            }

            if (!hoursByDay.TryGetValue(dayOffset, out var list))
            {
                list = [];
                hoursByDay[dayOffset] = list;
            }

            list.Add(h);
        }

        return hoursByDay;
    }

    private static Dictionary<ParameterDef, double?> MedianMeans(IReadOnlyList<ParameterConsensus> parameters, List<int> hours) =>
        parameters.ToDictionary(p => p.Parameter, p => Mean(hours.Select(h => p.MedianAt(Horizon.Hours(h)))));

    private static double? SunshinePercent(HourlyConsensus hourly, ParameterConsensus? isDay, List<int> hours)
    {
        if (hourly.Find(ParameterRegistry.SunshineDuration) is not { } sunshine || isDay is null)
        {
            return null;
        }

        var totalSunshine = 0.0;
        var totalDaylight = 0.0;
        foreach (var horizon in hours.Select(Horizon.Hours))
        {
            if (sunshine.MedianAt(horizon) is { } seconds && isDay.MedianAt(horizon) is > 0.5)
            {
                totalSunshine += seconds;
                totalDaylight += SecondsPerHour;
            }
        }

        return totalDaylight > 0 ? Math.Round(totalSunshine / totalDaylight * 100, 1) : null;
    }

    private static ConfidenceBounds? Bounds(HourlyConsensus hourly, List<int> dayHours, List<int> nightHours, List<int> allHours)
    {
        if (hourly.Find(ParameterRegistry.Temperature2m) is not { } temperature
            || !temperature.ByHorizon.Values.Any(hc => hc.AvailableModels.Count >= 2))
        {
            return null;
        }

        PeriodMeans BoundMeans(bool pessimistic) => new(
            CiBoundMeans(hourly, dayHours, pessimistic),
            CiBoundMeans(hourly, nightHours, pessimistic),
            CiBoundMeans(hourly, allHours, pessimistic));

        return new ConfidenceBounds(BoundMeans(pessimistic: true), BoundMeans(pessimistic: false), Agreement(hourly, allHours));
    }

    private static Dictionary<ParameterDef, double?> CiBoundMeans(HourlyConsensus hourly, List<int> hours, bool pessimistic) =>
        BoundParameters.ToDictionary(p => p, p =>
            hourly.Find(p) is { } series && hours.Count > 0
                ? Mean(hours.Select(h => CiBound(series.At(Horizon.Hours(h)), lower: HigherIsBetter.Contains(p) == pessimistic)))
                : null);

    private static double? CiBound(HorizonConsensus? hc, bool lower) => hc switch
    {
        null => null,
        { ConfidenceInterval: { } ci } => lower ? ci.Lower : ci.Upper,
        { Median: { } median, Spread: { } spread } => lower ? median - spread / 2 : median + spread / 2,
        _ => hc.Median,
    };

    private static double Agreement(HourlyConsensus hourly, List<int> hours)
    {
        double total = 0;
        var count = 0;
        foreach (var series in AgreementParameters.Select(hourly.Find).OfType<ParameterConsensus>())
        {
            foreach (var h in hours)
            {
                if (series.At(Horizon.Hours(h))?.Agreement is { } agreement)
                {
                    total += agreement;
                    count++;
                }
            }
        }

        return count > 0 ? Math.Round(total / count, 3) : 0;
    }

    private static double? Mean(IEnumerable<double?> values)
    {
        double sum = 0;
        var count = 0;
        foreach (var value in values)
        {
            if (value is { } v)
            {
                sum += v;
                count++;
            }
        }

        return count > 0 ? sum / count : null;
    }
}
