using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class ConditionValueMappingExtensions
{
    public static Proto.ConditionValue ToConditionValue(this ConditionFeatureResult r)
    {
        return new Proto.ConditionValue { Active = true };
    }
}
