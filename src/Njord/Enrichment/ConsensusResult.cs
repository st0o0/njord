using System.Text.Json.Nodes;
using Njord.Domain;
using Njord.Egress;

namespace Njord.Enrichment;

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
                var targetTime = StatePayloadBuilder.Anchor(now, hours);
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

    public IReadOnlyList<MqttMessage> ToMqttMessages(string baseTopic, string location)
    {
        var messages = new List<MqttMessage>();
        var horizonPayloads = new Dictionary<string, JsonObject>();

        foreach (var pc in Parameters)
        {
            foreach (var (horizon, hc) in pc.ByHorizon)
            {
                if (!horizonPayloads.TryGetValue(horizon, out var payload))
                {
                    payload = new JsonObject();
                    horizonPayloads[horizon] = payload;
                }

                payload[pc.Parameter.JsonKey] = hc.Median.HasValue
                    ? JsonValue.Create(Math.Round(hc.Median.Value, 2))
                    : null;
            }
        }

        foreach (var (horizon, payload) in horizonPayloads)
        {
            var firstParam = Parameters.FirstOrDefault()?.ByHorizon.GetValueOrDefault(horizon);
            if (firstParam is not null)
            {
                payload["_spread"] = firstParam.Spread.HasValue
                    ? JsonValue.Create(Math.Round(firstParam.Spread.Value, 2))
                    : null;
                payload["_agreement"] = firstParam.Agreement.HasValue
                    ? JsonValue.Create(Math.Round(firstParam.Agreement.Value, 3))
                    : null;
                payload["_models_used"] = firstParam.AvailableModels.Count;
            }

            var topic = TopicScheme.ConsensusHorizonTopic(baseTopic, location, horizon);
            messages.Add(new MqttMessage(topic, payload.ToJsonString(), true));
        }

        return messages;
    }
}
