using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Indices;
using Njord.Model;

namespace Njord.Indices;

public static class FrostProtection
{
    private const int MaxHours = 48;
    private static readonly Horizon ConfidenceHorizon = Horizon.Hours(3);

    public static FrostProtectionInfo? Compute(LocationConsensus consensus)
    {
        if (consensus.Hourly.Find(ParameterRegistry.Temperature2m) is not { } temperature)
        {
            return null;
        }

        var lastHour = Math.Min(consensus.Hourly.CutoffHour, MaxHours);
        int? firstFrostHours = null;
        var totalHours = 0;
        for (var h = 0; h <= lastHour; h++)
        {
            if (temperature.MedianAt(Horizon.Hours(h)) is not { } median)
            {
                continue;
            }

            totalHours++;
            if (median <= 0)
            {
                firstFrostHours ??= h;
            }
        }

        if (firstFrostHours is null)
        {
            return null;
        }

        var confidence = temperature.At(ConfidenceHorizon)?.Agreement ?? (totalHours > 0 ? 1.0 : 0.0);
        return new FrostProtectionInfo(firstFrostHours.Value, Math.Round(confidence, 2));
    }
}
