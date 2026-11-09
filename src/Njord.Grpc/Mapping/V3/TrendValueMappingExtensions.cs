using Njord.Messages.Results;

namespace Njord.Grpc.Mapping.V3;

internal static class TrendValueMappingExtensions
{
    public static Grpc.V3.TrendValue ToTrendValue(this TrendFeatureResult r)
    {
        return new Grpc.V3.TrendValue();
    }
}
