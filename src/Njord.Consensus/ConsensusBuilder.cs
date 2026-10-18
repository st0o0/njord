using Njord.Consensus.Settings;
using Njord.Forecast;
using Njord.Model;

namespace Njord.Consensus;

public sealed class ConsensusBuilder(ResolvedParameterSet parameters, TimeProvider timeProvider)
{
    public LocationConsensus Create(
        ModelForecasts forecasts,
        string location,
        ConsensusSettings settings)
    {
        const double agreementTolerance = 2.0;
        var trimPercent = settings.TrimPercent;
        var now = timeProvider.GetUtcNow();

        var cutoffHour = ComputeCutoffHour(forecasts, location, now);
        var hourlyResults = cutoffHour >= 0
            ? ComputeHourly(forecasts, parameters.Hourly, cutoffHour, location, now, trimPercent, agreementTolerance)
            : [];

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var cutoffDay = ComputeDailyCutoff(forecasts, location);
        var dailyResults = cutoffDay > 0
            ? ComputeDaily(forecasts, parameters.Daily, cutoffDay, location, today, trimPercent, agreementTolerance)
            : [];

        var hourly = new HourlyConsensus(
            FilterParameterList(hourlyResults, settings.MinQuorum),
            cutoffHour);

        var daily = new DailyConsensus(
            FilterParameterList(dailyResults, settings.MinQuorum),
            cutoffDay);

        return new LocationConsensus(new LocationId(location), hourly, daily, now);
    }

    private static int ComputeCutoffHour(ModelForecasts forecasts, string location, DateTimeOffset now)
    {
        var modelCount = forecasts.Entries.Count(e => e.Key.Location == location);
        var maxHours = new List<int>(modelCount);

        foreach (var (key, forecast) in forecasts.Entries)
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

    private static int ComputeDailyCutoff(ModelForecasts forecasts, string location)
    {
        var modelCount = forecasts.Entries.Count(e => e.Key.Location == location);
        var dayCounts = new List<int>(modelCount);

        foreach (var (key, forecast) in forecasts.Entries)
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
        ModelForecasts forecasts,
        IReadOnlyList<ParameterDef> hourlyParams,
        int cutoffHour,
        string location,
        DateTimeOffset now,
        double trimPercent,
        double agreementTolerance)
    {
        var modelCount = forecasts.Entries.Count(e => e.Key.Location == location);
        var pointIndex = new Dictionary<(string Location, WeatherModel Model), Dictionary<DateTimeOffset, ForecastPoint>>(modelCount);
        foreach (var (key, forecast) in forecasts.Entries)
        {
            if (key.Location == location)
            {
                pointIndex[key] = forecast.Hourly.Points.ToDictionary(p => p.ValidAt);
            }
        }

        var paramResults = new List<ParameterConsensus>(hourlyParams.Count);

        foreach (var parameter in hourlyParams)
        {
            var byHorizon = new Dictionary<Horizon, HorizonConsensus>(cutoffHour + 1);

            for (var hours = 0; hours <= cutoffHour; hours++)
            {
                var targetTime = TimeAnchor.AtHorizon(now, hours);
                var horizonKey = Horizon.Hours(hours);

                var modelValues = new List<(WeatherModel Model, double? Value)>(modelCount);
                foreach (var (key, _) in forecasts.Entries)
                {
                    if (key.Location != location)
                    {
                        continue;
                    }

                    pointIndex.TryGetValue(key, out var pointsByValidAt);
                    ForecastPoint? point = null;
                    pointsByValidAt?.TryGetValue(targetTime, out point);
                    modelValues.Add((key.Model, point?.Get(parameter).Number));
                }

                byHorizon[horizonKey] = ComputeHorizon(modelValues, trimPercent, agreementTolerance);
            }

            paramResults.Add(new ParameterConsensus(parameter, byHorizon));
        }

        return paramResults;
    }

    private static List<ParameterConsensus> ComputeDaily(
        ModelForecasts forecasts,
        IReadOnlyList<ParameterDef> dailyParams,
        int maxDays,
        string location,
        DateOnly today,
        double trimPercent,
        double agreementTolerance)
    {
        var modelCount = forecasts.Entries.Count(e => e.Key.Location == location);
        var paramResults = new List<ParameterConsensus>(dailyParams.Count);

        foreach (var parameter in dailyParams)
        {
            var byHorizon = new Dictionary<Horizon, HorizonConsensus>(maxDays);

            for (var day = 0; day < maxDays; day++)
            {
                var targetDate = today.AddDays(day);
                var horizonKey = Horizon.Days(day);

                var modelValues = new List<(WeatherModel Model, double? Value)>(modelCount);
                foreach (var (key, forecast) in forecasts.Entries)
                {
                    if (key.Location != location)
                    {
                        continue;
                    }

                    var point = forecast.Daily.Points.FirstOrDefault(p => p.Date == targetDate);
                    modelValues.Add((key.Model, point?.Get(parameter).Number));
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
        var median = ConsensusStatistics.ComputeMedian(values);
        var trimmedMean = ConsensusStatistics.ComputeTrimmedMean(values, trimPercent);
        var spread = ConsensusStatistics.ComputeSpread(values);
        var iqr = ConsensusStatistics.ComputeIqr(values);
        var agreement = median.HasValue
            ? ConsensusStatistics.ComputeAgreement(values, median.Value, agreementTolerance)
            : null;
        var outlierTuple = median.HasValue
            ? ConsensusStatistics.IdentifyOutlier(modelValues, median.Value)
            : null;
        var outlier = outlierTuple.HasValue
            ? new OutlierInfo(outlierTuple.Value.Model, outlierTuple.Value.Deviation)
            : null;
        var ciTuple = ConsensusStatistics.ComputeConfidenceInterval(values, 10, 90);
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
            var filteredHorizons = new Dictionary<Horizon, HorizonConsensus>();
            foreach (var (key, hc) in pc.ByHorizon)
            {
                if (hc.AvailableModels.Count >= minModels)
                {
                    filteredHorizons[key] = hc;
                }
            }

            if (filteredHorizons.Count > 0)
            {
                filtered.Add(pc with { ByHorizon = filteredHorizons });
            }
        }

        return filtered;
    }
}
