using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record DailyConsensusSummary(
    DateOnly Date,
    double? TemperatureMax,
    double? TemperatureMin,
    double? PrecipitationSum,
    double? WindSpeedMax,
    int? WeatherCode,
    double? Spread,
    double? Agreement,
    int AvailableModels)
{
    public static IReadOnlyList<DailyConsensusSummary> Aggregate(
        ConsensusResult result, DateTimeOffset now)
    {
        var temperature = FindParameter(result.Parameters, "temperature_2m");
        var precipitation = FindParameter(result.Parameters, "precipitation");
        var windSpeed = FindParameter(result.Parameters, "wind_speed_10m");
        var weatherCode = FindParameter(result.Parameters, "weather_code");

        if (temperature is null)
            return [];

        var dayGroups = GroupHorizonsByDay(temperature, now);
        var summaries = new List<DailyConsensusSummary>(dayGroups.Count);

        foreach (var (date, horizonKeys) in dayGroups.OrderBy(kv => kv.Key))
        {
            var tempMedians = CollectValues(temperature, horizonKeys, h => h.Median);
            var tempMax = tempMedians.Count > 0 ? tempMedians.Max() : (double?)null;
            var tempMin = tempMedians.Count > 0 ? tempMedians.Min() : (double?)null;

            var precipMedians = precipitation is not null
                ? CollectValues(precipitation, horizonKeys, h => h.Median)
                : [];
            var precipSum = precipMedians.Count > 0 ? precipMedians.Sum() : (double?)null;

            var windMedians = windSpeed is not null
                ? CollectValues(windSpeed, horizonKeys, h => h.Median)
                : [];
            var windMax = windMedians.Count > 0 ? windMedians.Max() : (double?)null;

            var wCode = weatherCode is not null
                ? FindNoonWeatherCode(weatherCode, horizonKeys, now, date)
                : (int?)null;

            var spreads = CollectValues(temperature, horizonKeys, h => h.Spread);
            var avgSpread = spreads.Count > 0 ? Math.Round(spreads.Average(), 2) : (double?)null;

            var agreements = CollectValues(temperature, horizonKeys, h => h.Agreement);
            var avgAgreement = agreements.Count > 0 ? Math.Round(agreements.Average(), 2) : (double?)null;

            var modelCounts = horizonKeys
                .Select(k => temperature.ByHorizon.TryGetValue(k, out var hc) ? hc.AvailableModels.Count : 0)
                .Where(c => c > 0)
                .ToList();
            var minModels = modelCounts.Count > 0 ? modelCounts.Min() : 0;

            summaries.Add(new DailyConsensusSummary(
                date, tempMax, tempMin, precipSum, windMax, wCode,
                avgSpread, avgAgreement, minModels));
        }

        return summaries;
    }

    private static ParameterConsensus? FindParameter(
        IReadOnlyList<ParameterConsensus> parameters, string apiName)
        => parameters.FirstOrDefault(p => p.Parameter.ApiName == apiName);

    private static Dictionary<DateOnly, List<string>> GroupHorizonsByDay(
        ParameterConsensus parameter, DateTimeOffset now)
    {
        var groups = new Dictionary<DateOnly, List<string>>();

        foreach (var (horizonKey, _) in parameter.ByHorizon)
        {
            if (!horizonKey.StartsWith('h') || !int.TryParse(horizonKey.AsSpan(1), out var hours))
                continue;

            var utcTime = TimeAnchor.AtHorizon(now, hours);
            var date = DateOnly.FromDateTime(utcTime.UtcDateTime);

            if (!groups.TryGetValue(date, out var list))
            {
                list = [];
                groups[date] = list;
            }

            list.Add(horizonKey);
        }

        return groups;
    }

    private static List<double> CollectValues(
        ParameterConsensus parameter, List<string> horizonKeys,
        Func<HorizonConsensus, double?> selector)
    {
        var values = new List<double>(horizonKeys.Count);
        foreach (var key in horizonKeys)
        {
            if (parameter.ByHorizon.TryGetValue(key, out var hc))
            {
                var v = selector(hc);
                if (v.HasValue)
                    values.Add(v.Value);
            }
        }
        return values;
    }

    private static int? FindNoonWeatherCode(
        ParameterConsensus weatherCode, List<string> horizonKeys,
        DateTimeOffset now, DateOnly date)
    {
        var noonUtc = new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero);

        string? bestKey = null;
        var bestDiff = double.MaxValue;

        foreach (var key in horizonKeys)
        {
            if (!key.StartsWith('h') || !int.TryParse(key.AsSpan(1), out var hours))
                continue;

            var utcTime = TimeAnchor.AtHorizon(now, hours);
            var diff = Math.Abs((utcTime - noonUtc).TotalMinutes);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestKey = key;
            }
        }

        if (bestKey is null || !weatherCode.ByHorizon.TryGetValue(bestKey, out var hc) || !hc.Median.HasValue)
            return null;

        return (int)Math.Round(hc.Median.Value);
    }
}
