using Njord.Messages.Results;


namespace Njord.Features.Shared;

public sealed record HourlyScore(DateTimeOffset Time, double Score, LimitingKind? WorstComponent = null, double? WorstValue = null);

public static class WindowScanner
{
    public static IReadOnlyList<ActivityWindow> Scan(
        IReadOnlyList<HourlyScore> scores,
        int minScore = 50,
        int minHours = 2,
        int maxWindows = 3)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        var runs = new List<(int Start, int End)>();
        int? runStart = null;
        for (var i = 0; i < scores.Count; i++)
        {
            if (scores[i].Score >= minScore)
            {
                runStart ??= i;
            }
            else if (runStart.HasValue)
            {
                runs.Add((runStart.Value, i - 1));
                runStart = null;
            }
        }

        if (runStart.HasValue)
        {
            runs.Add((runStart.Value, scores.Count - 1));
        }

        var windows = new List<ActivityWindow>();
        foreach (var (start, end) in runs)
        {
            if (end - start + 1 < minHours)
            {
                continue;
            }

            var bestStart = start;
            var bestMinScore = double.MinValue;
            for (var i = start; i <= end - minHours + 1; i++)
            {
                var windowMin = double.MaxValue;
                for (var j = i; j < i + minHours; j++)
                {
                    windowMin = Math.Min(windowMin, scores[j].Score);
                }

                if (windowMin > bestMinScore)
                {
                    bestMinScore = windowMin;
                    bestStart = i;
                }
            }

            var windowStart = bestStart;
            var windowEnd = bestStart + minHours - 1;
            while (windowEnd < end && scores[windowEnd + 1].Score >= minScore)
            {
                windowEnd++;
            }

            while (windowStart > start && scores[windowStart - 1].Score >= minScore)
            {
                windowStart--;
            }

            var windowScore = (int)scores.Skip(windowStart).Take(windowEnd - windowStart + 1).Min(s => s.Score);

            LimitingFactor? limiting = null;
            if (windowEnd + 1 < scores.Count)
            {
                var boundary = scores[windowEnd + 1];
                if (boundary.WorstComponent.HasValue)
                {
                    limiting = new LimitingFactor(boundary.WorstComponent.Value, boundary.Time, boundary.WorstValue ?? 0);
                }
            }

            windows.Add(new ActivityWindow(
                scores[windowStart].Time,
                scores[windowEnd].Time.AddHours(1),
                windowScore,
                limiting));
        }

        return [.. windows.OrderByDescending(w => w.Score).Take(maxWindows)];
    }
}
