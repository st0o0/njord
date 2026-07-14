using Njord.Configuration;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record HistoryResult(
    string Location,
    Dictionary<WeatherModel, double?> Mae7d,
    Dictionary<WeatherModel, double?> Mae30d,
    Dictionary<WeatherModel, double> Weights,
    Dictionary<WeatherModel, double?> Drift,
    WeatherModel? SeasonalBest,
    (bool IsAnomaly, double DeviationSigma)? Anomaly,
    double? WeightedTemperature)
{
    public static HistoryResult Compute(
        ForecastHistory history,
        ModelSnapshot current,
        string location,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        HistoryOptions options)
    {
        var now = timeProvider.GetUtcNow();
        var tempApiName = ParameterRegistry.Temperature2m.ApiName;

        var mae7d = HistoryAnalyzer.ModelAccuracy(history, tempApiName, 7, options.MinSampleSize);
        var mae30d = HistoryAnalyzer.ModelAccuracy(history, tempApiName, 30, options.MinSampleSize);
        var weights = HistoryAnalyzer.ModelWeights(mae30d);

        var drift = new Dictionary<WeatherModel, double?>();
        foreach (var model in mae30d.Keys)
        {
            drift[model] = HistoryAnalyzer.ForecastDrift(history, model, tempApiName);
        }

        var seasonalBest = HistoryAnalyzer.SeasonalPreference(history, tempApiName, now, options.MinSampleSize);

        (bool, double)? anomaly = null;
        var currentConsensus = current.Entries
            .Where(e => e.Key.Location == location)
            .Select(e => e.Value)
            .ToList();

        if (currentConsensus.Count > 0)
        {
            var tempParam = parameters.Get(ParameterRegistry.Temperature2m);
            if (tempParam is not null)
            {
                var nearestPoint = currentConsensus[0].Hourly.Points
                    .OrderBy(p => Math.Abs((p.ValidAt - now).TotalMinutes))
                    .FirstOrDefault();
                var currentTemp = nearestPoint?.Get(tempParam);

                if (currentTemp.HasValue)
                {
                    var values = currentConsensus
                        .Select(f =>
                        {
                            var pt = f.Hourly.Points
                                .OrderBy(p => Math.Abs((p.ValidAt - now).TotalMinutes))
                                .FirstOrDefault();
                            return pt?.Get(tempParam);
                        })
                        .ToList();
                    var median = ConsensusComputer.ComputeMedian(values);

                    if (median.HasValue)
                        anomaly = HistoryAnalyzer.AnomalyDetection(history, tempApiName, median.Value, now.Hour);
                }
            }
        }

        double? weightedTemp = null;
        var modelValues = new List<(WeatherModel, double?)>();
        foreach (var (key, forecast) in current.Entries)
        {
            if (key.Location != location) continue;
            var tempParam = parameters.Get(ParameterRegistry.Temperature2m);
            if (tempParam is null) continue;
            var pt = forecast.Hourly.Points
                .OrderBy(p => Math.Abs((p.ValidAt - now).TotalMinutes))
                .FirstOrDefault();
            modelValues.Add((key.Model, pt?.Get(tempParam)));
        }
        if (modelValues.Count > 0)
            weightedTemp = HistoryAnalyzer.WeightedConsensus(modelValues, weights);

        return new HistoryResult(location, mae7d, mae30d, weights, drift, seasonalBest, anomaly, weightedTemp);
    }
}
