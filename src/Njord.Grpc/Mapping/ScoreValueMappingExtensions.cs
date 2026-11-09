using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class ScoreValueMappingExtensions
{
    public static Proto.ScoreValue ToScoreValue(this ScoreFeatureResult r)
    {
        return new Proto.ScoreValue
        {
            Score = r.Score,
            BestStart = Timestamp.FromDateTimeOffset(r.BestStart),
            BestHours = r.BestHours,
        };
    }
}
