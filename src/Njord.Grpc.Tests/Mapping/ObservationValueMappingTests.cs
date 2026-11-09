using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ObservationValueMappingTests
{
    [Fact]
    public void Value_maps()
    {
        var result = new ObservationResult(new Location("Vienna", 48.2, 16.37), "TestObservation", 21.5);

        Assert.Equal(21.5, result.ToObservationValue().Value);
    }
}
