using Njord.Messages.Results;

namespace Njord.Grpc.Mapping.V3;

internal static class ConditionValueMappingExtensions
{
    public static Grpc.V3.ConditionValue ToConditionValue(this ConditionFeatureResult r)
    {
        return new Grpc.V3.ConditionValue { Active = true };
    }
}
