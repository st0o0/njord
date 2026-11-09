using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results.Forecasts;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ForecastValueMappingTests
{
    [Fact]
    public void Forecast_result_maps_to_value()
    {
        var result = new ForecastModelComparisonResult(new Location("Vienna", 48.2, 16.37), []);

        Assert.NotNull(result.ToForecastValue());
    }
}
