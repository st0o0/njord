using Njord.Messages.Results;

namespace Njord.Grpc.Mapping.V3;

internal static class ObservationValueMappingExtensions
{
    public static Grpc.V3.ObservationValue ToObservationValue(this ObservationResult r)
    {
        return new Grpc.V3.ObservationValue { Value = r.Value };
    }
}
