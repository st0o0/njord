using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record HorizonConsensus(
    double? Median,
    double? TrimmedMean,
    double? Spread,
    double? Iqr,
    double? Agreement,
    (WeatherModel Model, double Deviation)? Outlier,
    (double Lower, double Upper)? ConfidenceInterval,
    IReadOnlyList<WeatherModel> AvailableModels);

public sealed record ParameterConsensus(
    ParameterDef Parameter,
    IReadOnlyDictionary<string, HorizonConsensus> ByHorizon);

public sealed record ConsensusResult(IReadOnlyList<ParameterConsensus> Parameters)
{
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
        var paramResults = new List<ParameterConsensus>();

        foreach (var parameter in parameters.Hourly)
        {
            var byHorizon = new Dictionary<string, HorizonConsensus>();

            foreach (var hours in horizons)
            {
                var targetTime = TimeAnchor.AtHorizon(now, hours);
                var horizonKey = $"h{hours}";

                var modelValues = new List<(WeatherModel Model, double? Value)>();
                foreach (var (key, forecast) in snapshot.Entries)
                {
                    if (key.Location != location) continue;
                    var point = forecast.Hourly.Points.FirstOrDefault(p =>
                        Math.Abs((p.ValidAt - targetTime).TotalMinutes) < 30);
                    modelValues.Add((key.Model, point?.Get(parameter)));
                }

                var values = modelValues.Select(mv => mv.Value).ToList();
                var median = ConsensusComputer.ComputeMedian(values);
                var trimmedMean = ConsensusComputer.ComputeTrimmedMean(values, trimPercent);
                var spread = ConsensusComputer.ComputeSpread(values);
                var iqr = ConsensusComputer.ComputeIqr(values);
                var agreement = median.HasValue
                    ? ConsensusComputer.ComputeAgreement(values, median.Value, agreementTolerance)
                    : null;
                var outlier = median.HasValue
                    ? ConsensusComputer.IdentifyOutlier(modelValues, median.Value)
                    : null;
                var ci = ConsensusComputer.ComputeConfidenceInterval(values, 10, 90);

                var availableModels = modelValues
                    .Where(mv => mv.Value.HasValue)
                    .Select(mv => mv.Model)
                    .ToList();

                byHorizon[horizonKey] = new HorizonConsensus(
                    median, trimmedMean, spread, iqr, agreement,
                    outlier, ci, availableModels);
            }

            paramResults.Add(new ParameterConsensus(parameter, byHorizon));
        }

        return new ConsensusResult(paramResults);
    }
}
