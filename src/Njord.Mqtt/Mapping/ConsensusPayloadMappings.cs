using Njord.Compute.Consensus;
using Njord.Compute.Querying;
using Njord.Mqtt.Payloads;
using Njord.Weather;

namespace Njord.Mqtt.Mapping;

internal static class ConsensusPayloadMappings
{
    private const int ValueDigits = 2;
    private const int AgreementDigits = 3;

    public static IReadOnlyList<(Horizon Horizon, ConsensusHorizonPayload Payload)> ToPayload(
        this IReadOnlyList<ParameterConsensus> parameters)
    {
        var byHorizon = new Dictionary<Horizon, List<ConsensusParameterPayload>>();

        foreach (var parameter in parameters)
        {
            foreach (var (horizon, consensus) in parameter.ByHorizon)
            {
                if (!byHorizon.TryGetValue(horizon, out var entries))
                {
                    entries = [];
                    byHorizon[horizon] = entries;
                }

                entries.Add(new ConsensusParameterPayload(
                    parameter.Parameter.ToMqttKey(),
                    Round(consensus.Median, ValueDigits),
                    consensus.AvailableModels.Count));
            }
        }

        var first = parameters.FirstOrDefault();
        return [.. byHorizon.Select(pair => (pair.Key, new ConsensusHorizonPayload(pair.Value, SpreadAt(first, pair.Key))))];
    }

    private static ConsensusSpreadPayload? SpreadAt(ParameterConsensus? first, Horizon horizon)
        => first.At(horizon) is { } consensus
            ? new ConsensusSpreadPayload(Round(consensus.Spread, ValueDigits), Round(consensus.Agreement, AgreementDigits))
            : null;

    private static double? Round(double? value, int digits)
        => value is { } number ? Math.Round(number, digits) : null;
}
