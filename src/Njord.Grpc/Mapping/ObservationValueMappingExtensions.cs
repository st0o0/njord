using Njord.Messages.Results;

namespace Njord.Grpc.Mapping;

internal static class ObservationValueMappingExtensions
{
    public static Proto.ObservationValue ToObservationValue(this ObservationResult r)
    {
        return new Proto.ObservationValue { Value = r.Value };
    }
}
