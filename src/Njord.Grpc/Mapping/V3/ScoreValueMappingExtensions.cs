using Google.Protobuf.WellKnownTypes;
using Njord.Messages.Results;

namespace Njord.Grpc.Mapping.V3;

internal static class ScoreValueMappingExtensions
{
    public static Grpc.V3.ScoreValue ToScoreValue(this ScoreFeatureResult r)
    {
        return new Grpc.V3.ScoreValue
        {
            Score = r.Score,
            BestStart = Timestamp.FromDateTimeOffset(r.BestStart),
            BestHours = r.BestHours,
        };
    }
}
