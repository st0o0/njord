using Njord.Domain;

namespace Njord.Enrichment;

public static class HistoryAnalyzer
{
    public static Dictionary<WeatherModel, double?> ModelAccuracy(
        ForecastHistory history, string paramApiName, int windowDays, int minSampleSize = 48)
    {
        var result = new Dictionary<WeatherModel, double?>();
        var now = history.Records.Count > 0 ? history.Records[^1].Timestamp : DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-windowDays);

        var records = history.Records.Where(r => r.Timestamp >= cutoff).ToList();

        var allModels = records
            .SelectMany(r => r.ModelValues.Keys)
            .Distinct()
            .ToList();

        foreach (var model in allModels)
        {
            var errors = new List<double>();

            foreach (var record in records)
            {
                if (!record.ModelValues.TryGetValue(model, out var modelVals)) continue;
                if (!modelVals.TryGetValue(paramApiName, out var forecast) || forecast is null) continue;
                if (!record.ConsensusValues.TryGetValue(paramApiName, out var observed) || observed is null) continue;

                errors.Add(Math.Abs(forecast.Value - observed.Value));
            }

            result[model] = errors.Count >= minSampleSize ? Math.Round(errors.Average(), 2) : null;
        }

        return result;
    }

    public static Dictionary<WeatherModel, double> ModelWeights(
        Dictionary<WeatherModel, double?> maeByModel)
    {
        var result = new Dictionary<WeatherModel, double>();
        var hasAnyMae = maeByModel.Values.Any(v => v.HasValue);

        if (!hasAnyMae)
        {
            var equalWeight = maeByModel.Count > 0 ? 1.0 / maeByModel.Count : 0;
            foreach (var model in maeByModel.Keys)
                result[model] = Math.Round(equalWeight, 4);
            return result;
        }

        var rawWeights = new Dictionary<WeatherModel, double>();
        double totalWeight = 0;

        foreach (var (model, mae) in maeByModel)
        {
            var w = 1.0 / ((mae ?? 1.0) + 0.1);
            rawWeights[model] = w;
            totalWeight += w;
        }

        foreach (var (model, w) in rawWeights)
            result[model] = Math.Round(w / totalWeight, 4);

        return result;
    }

    public static double? WeightedConsensus(
        IReadOnlyList<(WeatherModel Model, double? Value)> modelValues,
        Dictionary<WeatherModel, double> weights)
    {
        double weightedSum = 0;
        double weightSum = 0;

        foreach (var (model, value) in modelValues)
        {
            if (value is not { } v) continue;
            if (!weights.TryGetValue(model, out var w)) continue;
            weightedSum += w * v;
            weightSum += w;
        }

        return weightSum > 0 ? Math.Round(weightedSum / weightSum, 2) : null;
    }

    public static double? ForecastDrift(
        ForecastHistory history, WeatherModel model, string paramApiName, int runCount = 5)
    {
        var modelRecords = history.Records
            .Where(r => r.ModelValues.ContainsKey(model))
            .TakeLast(runCount)
            .ToList();

        var values = new List<double>();
        foreach (var record in modelRecords)
        {
            if (!record.ModelValues[model].TryGetValue(paramApiName, out var val) || val is null) continue;
            values.Add(val.Value);
        }

        if (values.Count < 2) return null;

        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Round(Math.Sqrt(variance), 2);
    }

    public static WeatherModel? SeasonalPreference(
        ForecastHistory history, string paramApiName, DateTimeOffset now, int minSampleSize = 48)
    {
        var season = GetSeason(now.Month);
        var seasonRecords = history.Records
            .Where(r => GetSeason(r.Timestamp.Month) == season)
            .ToList();

        if (seasonRecords.Count < minSampleSize) return null;

        var seasonHistory = new ForecastHistory(365);
        foreach (var r in seasonRecords) seasonHistory.Add(r);

        var maes = ModelAccuracy(seasonHistory, paramApiName, 365, minSampleSize);
        return maes
            .Where(kv => kv.Value.HasValue)
            .OrderBy(kv => kv.Value!.Value)
            .Select(kv => kv.Key)
            .FirstOrDefault();
    }

    public static (bool IsAnomaly, double DeviationSigma)? AnomalyDetection(
        ForecastHistory history, string paramApiName, double currentValue, int hourOfDay, int minRecords = 30)
    {
        var hourRecords = history.Records
            .Where(r => r.Timestamp.Hour == hourOfDay && r.ConsensusValues.TryGetValue(paramApiName, out var v) && v.HasValue)
            .Select(r => r.ConsensusValues[paramApiName]!.Value)
            .ToList();

        if (hourRecords.Count < minRecords) return null;

        var mean = hourRecords.Average();
        var variance = hourRecords.Sum(v => (v - mean) * (v - mean)) / hourRecords.Count;
        var stdDev = Math.Sqrt(variance);

        if (stdDev < 0.001) return (false, 0);

        var deviation = Math.Round(Math.Abs(currentValue - mean) / stdDev, 2);
        return (deviation > 2.0, deviation);
    }

    private static string GetSeason(int month) => month switch
    {
        >= 3 and <= 5 => "spring",
        >= 6 and <= 8 => "summer",
        >= 9 and <= 11 => "autumn",
        _ => "winter",
    };
}
