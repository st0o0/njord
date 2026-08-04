using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public static class TimeSliceAggregator
{
    public static IReadOnlyList<DaySlice> AggregateDaySlices(
        ConsensusSnapshot consensus, ResolvedParameterSet parameters, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        var todayMidnight = now.UtcDateTime.Date;
        var cutoffHour = consensus.Hourly.CutoffHour;

        if (cutoffHour < 0)
            return [];

        var isDayParam = FindParam(consensus.Hourly.Parameters,
            parameters.Get(ParameterRegistry.IsDay));

        var hoursByDay = new Dictionary<int, List<int>>();

        for (var h = 0; h <= cutoffHour; h++)
        {
            var absoluteTime = now.AddHours(h).UtcDateTime;
            var dayOffset = (int)Math.Floor((absoluteTime - todayMidnight).TotalDays);

            if (dayOffset > 2)
                break;

            if (!hoursByDay.TryGetValue(dayOffset, out var list))
            {
                list = [];
                hoursByDay[dayOffset] = list;
            }

            list.Add(h);
        }

        var scoringParams = consensus.Hourly.Parameters;
        var result = new List<DaySlice>();

        foreach (var dayOffset in hoursByDay.Keys.Order())
        {
            var hours = hoursByDay[dayOffset];
            var dayHours = new List<int>();
            var nightHours = new List<int>();

            foreach (var h in hours)
            {
                var isDayValue = isDayParam?.ByHorizon.GetValueOrDefault($"h{h}")?.Median;
                if (isDayParam is null || isDayValue is > 0.5)
                    dayHours.Add(h);
                else
                    nightHours.Add(h);
            }

            var dayMeans = ComputeMeans(scoringParams, dayHours);
            var nightMeans = ComputeMeans(scoringParams, nightHours);
            var fullDayMeans = ComputeMeans(scoringParams, hours);

            result.Add(new DaySlice(dayOffset, dayMeans, nightMeans, fullDayMeans,
                dayHours.Count, nightHours.Count));
        }

        return result;
    }

    private static Dictionary<ParameterDef, double?> ComputeMeans(
        IReadOnlyList<ParameterConsensus> parameters, List<int> hours)
    {
        var means = new Dictionary<ParameterDef, double?>();

        foreach (var param in parameters)
        {
            double sum = 0;
            var count = 0;

            foreach (var h in hours)
            {
                var median = param.ByHorizon.GetValueOrDefault($"h{h}")?.Median;
                if (median is { } v)
                {
                    sum += v;
                    count++;
                }
            }

            means[param.Parameter] = count > 0 ? sum / count : null;
        }

        return means;
    }

    private static ParameterConsensus? FindParam(
        IReadOnlyList<ParameterConsensus> parameters, ParameterDef? param)
        => param is null ? null : parameters.FirstOrDefault(p => p.Parameter == param);
}
