using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class TrendValueMappingExtensions
{
    public static Proto.TrendValue ToTrendValue(this TrendFeatureResult r)
    {
        return new Proto.TrendValue();
    }
}
