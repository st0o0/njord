using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed class ConsensusSnapshotFactory(ResolvedParameterSet parameters, TimeProvider timeProvider)
{
    public ConsensusSnapshot Create(
        ModelSnapshot snapshot,
        string location,
        double trimPercent = 0.1,
        double agreementTolerance = 2.0)
    {
        var now = timeProvider.GetUtcNow();

        var cutoffHour = ComputeCutoffHour(snapshot, location, now);
        var hourlyResults = cutoffHour >= 0
            ? ComputeHourly(snapshot, parameters.Hourly, cutoffHour, location, now, trimPercent, agreementTolerance)
            : [];

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var cutoffDay = ComputeDailyCutoff(snapshot, location);
        var dailyResults = cutoffDay > 0
            ? ComputeDaily(snapshot, parameters.Daily, cutoffDay, location, today, trimPercent, agreementTolerance)
            : [];

        var hourly = new HourlyConsensus(
            FilterParameterList(hourlyResults, minModels: 2),
            cutoffHour);

        var daily = new DailyConsensus(
            FilterParameterList(dailyResults, minModels: 2),
            cutoffDay);

        return new ConsensusSnapshot(location, hourly, daily, now);
    }

    private static int ComputeCutoffHour(ModelSnapshot snapshot, string location, DateTimeOffset now)
    {
        var maxHours = new List<int>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var lastPoint = forecast.Hourly.Points.LastOrDefault();
            if (lastPoint is null)
            {
                continue;
            }

            var hours = (int)Math.Floor((lastPoint.ValidAt - now).TotalHours);
            if (hours > 0)
            {
                maxHours.Add(hours);
            }
        }

        if (maxHours.Count < 2)
        {
            return -1;
        }

        maxHours.Sort();
        return maxHours[^2];
    }

    private static int ComputeDailyCutoff(ModelSnapshot snapshot, string location)
    {
        var dayCounts = new List<int>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var count = forecast.Daily.Points.Count;
            if (count > 0)
            {
                dayCounts.Add(count);
            }
        }

        if (dayCounts.Count < 2)
        {
            return 0;
        }

        dayCounts.Sort();
        return dayCounts[^2];
    }

    private static List<ParameterConsensus> ComputeHourly(
        ModelSnapshot snapshot,
        IReadOnlyList<ParameterDef> hourlyParams,
        int cutoffHour,
        string location,
        DateTimeOffset now,
        double trimPercent,
        double agreementTolerance)
    {
        var pointIndex = new Dictionary<(string Location, WeatherModel Model), Dictionary<DateTimeOffset, ForecastPoint>>();
        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location == location)
            {
                pointIndex[key] = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
            }
        }

        var paramResults = new List<ParameterConsensus>();

        foreach (var parameter in hourlyParams)
        {
            var byHorizon = new Dictionary<string, HorizonConsensus>();

            for (var hours = 0; hours <= cutoffHour; hours++)
            {
                var targetTime = TimeAnchor.AtHorizon(now, hours);
                var horizonKey = $"h{hours}";

                var modelValues = new List<(WeatherModel Model, double? Value)>();
                foreach (var (key, _) in snapshot.Entries)
                {
                    if (key.Location != location)
                    {
                        continue;
                    }

                    pointIndex.TryGetValue(key, out var pointsByValidAt);
                    ForecastPoint? point = null;
                    pointsByValidAt?.TryGetValue(targetTime, out point);
                    modelValues.Add((key.Model, point?.Get(parameter)));
                }

                byHorizon[horizonKey] = ComputeHorizon(modelValues, trimPercent, agreementTolerance);
            }

            paramResults.Add(new ParameterConsensus(parameter, byHorizon));
        }

        return paramResults;
    }

    private static List<ParameterConsensus> ComputeDaily(
        ModelSnapshot snapshot,
        IReadOnlyList<ParameterDef> dailyParams,
        int maxDays,
        string location,
        DateOnly today,
        double trimPercent,
        double agreementTolerance)
    {
        var paramResults = new List<ParameterConsensus>();

        foreach (var parameter in dailyParams)
        {
            var byHorizon = new Dictionary<string, HorizonConsensus>();

            for (var day = 0; day < maxDays; day++)
            {
                var targetDate = today.AddDays(day);
                var horizonKey = $"d{day}";

                var modelValues = new List<(WeatherModel Model, double? Value)>();
                foreach (var (key, forecast) in snapshot.Entries)
                {
                    if (key.Location != location)
                    {
                        continue;
                    }

                    var point = forecast.Daily.Points.FirstOrDefault(p => p.Date == targetDate);
                    modelValues.Add((key.Model, point?.GetNumeric(parameter)));
                }

                byHorizon[horizonKey] = ComputeHorizon(modelValues, trimPercent, agreementTolerance);
            }

            paramResults.Add(new ParameterConsensus(parameter, byHorizon));
        }

        return paramResults;
    }

    private static HorizonConsensus ComputeHorizon(
        List<(WeatherModel Model, double? Value)> modelValues,
        double trimPercent,
        double agreementTolerance)
    {
        var values = modelValues.Select(mv => mv.Value).ToList();
        var median = ConsensusComputer.ComputeMedian(values);
        var trimmedMean = ConsensusComputer.ComputeTrimmedMean(values, trimPercent);
        var spread = ConsensusComputer.ComputeSpread(values);
        var iqr = ConsensusComputer.ComputeIqr(values);
        var agreement = median.HasValue
            ? ConsensusComputer.ComputeAgreement(values, median.Value, agreementTolerance)
            : null;
        var outlierTuple = median.HasValue
            ? ConsensusComputer.IdentifyOutlier(modelValues, median.Value)
            : null;
        var outlier = outlierTuple.HasValue
            ? new OutlierInfo(outlierTuple.Value.Model, outlierTuple.Value.Deviation)
            : null;
        var ciTuple = ConsensusComputer.ComputeConfidenceInterval(values, 10, 90);
        var ci = ciTuple.HasValue
            ? new ConfidenceIntervalInfo(ciTuple.Value.Lower, ciTuple.Value.Upper)
            : null;

        var availableModels = modelValues
            .Where(mv => mv.Value.HasValue)
            .Select(mv => mv.Model)
            .ToList();

        return new HorizonConsensus(
            median, trimmedMean, spread, iqr, agreement,
            outlier, ci, availableModels);
    }

    private static List<ParameterConsensus> FilterParameterList(
        List<ParameterConsensus> parameters, int minModels)
    {
        var filtered = new List<ParameterConsensus>();

        foreach (var pc in parameters)
        {
            var filteredHorizons = new Dictionary<string, HorizonConsensus>();
            foreach (var (key, hc) in pc.ByHorizon)
            {
                if (hc.AvailableModels.Count >= minModels)
                {
                    filteredHorizons[key] = hc;
                }
            }

            if (filteredHorizons.Count > 0)
            {
                filtered.Add(new ParameterConsensus(pc.Parameter, filteredHorizons));
            }
        }

        return filtered;
    }
}
