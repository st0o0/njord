using Njord.Model;

namespace Njord.Core.Domain;

public static class ForecastQueries
{
    public static T? ValueAt<T>(DomainForecast<T> forecast, DateTimeOffset time)
        where T : class, IHasValidTime
    {
        var span = forecast.Resolution.ToTimeSpan();
        foreach (var point in forecast.Points)
        {
            if (point.ValidFrom <= time && time < point.ValidFrom + span)
            {
                return point;
            }
        }

        return null;
    }

    public static IReadOnlyList<T> Window<T>(DomainForecast<T> forecast, DateTimeOffset from, DateTimeOffset to)
        where T : IHasValidTime
    {
        var result = new List<T>();
        foreach (var point in forecast.Points)
        {
            if (point.ValidFrom >= from && point.ValidFrom <= to)
            {
                result.Add(point);
            }
        }

        return result;
    }

    public static T? Nearest<T>(DomainForecast<T> forecast, DateTimeOffset time, TimeSpan maxGap)
        where T : class, IHasValidTime
    {
        T? best = null;
        var bestGap = TimeSpan.MaxValue;
        foreach (var point in forecast.Points)
        {
            var gap = (point.ValidFrom - time).Duration();
            if (gap <= maxGap && gap < bestGap)
            {
                best = point;
                bestGap = gap;
            }
        }

        return best;
    }

    public static IReadOnlyList<(T1 First, T2 Second)> Correlate<T1, T2>(
        DomainForecast<T1> first,
        DomainForecast<T2> second)
        where T1 : IHasValidTime
        where T2 : IHasValidTime
    {
        var firstSpan = first.Resolution.ToTimeSpan();
        var secondSpan = second.Resolution.ToTimeSpan();
        var result = new List<(T1, T2)>();
        foreach (var a in first.Points)
        {
            var aEnd = a.ValidFrom + firstSpan;
            foreach (var b in second.Points)
            {
                if (a.ValidFrom < b.ValidFrom + secondSpan && b.ValidFrom < aEnd)
                {
                    result.Add((a, b));
                }
            }
        }

        return result;
    }
}
