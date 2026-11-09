using Njord.Core;
using Njord.Grpc.Mapping.V3;
using Njord.Messages.Results.Trends;

namespace Njord.Grpc.Tests.Mapping.V3;

public sealed class TrendValueMappingTests
{
    [Fact]
    public void Trend_result_maps_to_value()
    {
        var result = new ConfidenceTrendResult(new Location("Vienna", 48.2, 16.37), ConfidenceTrendDirection.Converging, 0.5, 1.0, 0.5);

        Assert.NotNull(result.ToTrendValue());
    }
}
