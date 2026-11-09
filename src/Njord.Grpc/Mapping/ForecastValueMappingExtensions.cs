using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class ForecastValueMappingExtensions
{
    public static Proto.ForecastValue ToForecastValue(this ForecastFeatureResult r)
    {
        return new Proto.ForecastValue();
    }
}
