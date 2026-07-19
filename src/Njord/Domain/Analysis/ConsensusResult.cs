using Newtonsoft.Json;
using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record OutlierInfo(
    [property: JsonProperty("model")] WeatherModel Model,
    [property: JsonProperty("deviation")] double Deviation);

public sealed record ConfidenceIntervalInfo(
    [property: JsonProperty("lower")] double Lower,
    [property: JsonProperty("upper")] double Upper);

public sealed record HorizonConsensus(
    [property: JsonProperty("median")] double? Median,
    [property: JsonProperty("trimmedMean")] double? TrimmedMean,
    [property: JsonProperty("spread")] double? Spread,
    [property: JsonProperty("iqr")] double? Iqr,
    [property: JsonProperty("agreement")] double? Agreement,
    [property: JsonProperty("outlier")] OutlierInfo? Outlier,
    [property: JsonProperty("confidenceInterval")] ConfidenceIntervalInfo? ConfidenceInterval,
    [property: JsonProperty("availableModels")] IReadOnlyList<WeatherModel> AvailableModels);

public sealed record ParameterConsensus(
    [property: JsonProperty("parameter")] ParameterDef Parameter,
    [property: JsonProperty("byHorizon")] IReadOnlyDictionary<string, HorizonConsensus> ByHorizon);

[method: JsonConstructor]
public sealed record ConsensusResult(
    [property: JsonProperty("parameters")] IReadOnlyList<ParameterConsensus> Parameters,
    [property: JsonProperty("dailyParameters")] IReadOnlyList<ParameterConsensus> DailyParameters)
{
    public ConsensusResult(IReadOnlyList<ParameterConsensus> parameters)
        : this(parameters, []) { }

    public static ConsensusResult Compute(
        ModelSnapshot snapshot,
        ResolvedParameterSet parameters,
        IReadOnlyList<int> horizons,
        string location,
        TimeProvider timeProvider,
        double trimPercent = 0.1,
        double agreementTolerance = 2.0)
    {
        var now = timeProvider.GetUtcNow();

        var hourlyResults = ComputeHourly(snapshot, parameters.Hourly, horizons, location, now, trimPercent, agreementTolerance);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var dailyHorizons = ComputeDailyCutoff(snapshot, location);
        var dailyResults = ComputeDaily(snapshot, parameters.Daily, dailyHorizons, location, today, trimPercent, agreementTolerance);

        return new ConsensusResult(hourlyResults, dailyResults);
    }

    private static List<ParameterConsensus> ComputeHourly(
        ModelSnapshot snapshot,
        IReadOnlyList<ParameterDef> hourlyParams,
        IReadOnlyList<int> horizons,
        string location,
        DateTimeOffset now,
        double trimPercent,
        double agreementTolerance)
    {
        var paramResults = new List<ParameterConsensus>();

        foreach (var parameter in hourlyParams)
        {
            var byHorizon = new Dictionary<string, HorizonConsensus>();

            foreach (var hours in horizons)
            {
                var targetTime = TimeAnchor.AtHorizon(now, hours);
                var horizonKey = $"h{hours}";

                var modelValues = new List<(WeatherModel Model, double? Value)>();
                foreach (var (key, forecast) in snapshot.Entries)
                {
                    if (key.Location != location)
                    {
                        continue;
                    }

                    var point = forecast.Hourly.Points.FirstOrDefault(p =>
                        Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
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

    private static int ComputeDailyCutoff(ModelSnapshot snapshot, string location)
    {
        var dayCounts = new List<int>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
                continue;

            var count = forecast.Daily.Points.Count;
            if (count > 0)
                dayCounts.Add(count);
        }

        if (dayCounts.Count < 2)
            return 0;

        dayCounts.Sort();
        return dayCounts[^2];
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
}
