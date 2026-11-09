using Njord.Messages.Results;

namespace Njord.Grpc.Mapping.V3;

internal static class ForecastValueMappingExtensions
{
    public static Grpc.V3.ForecastValue ToForecastValue(this ForecastFeatureResult r)
    {
        return new Grpc.V3.ForecastValue();
    }
}
