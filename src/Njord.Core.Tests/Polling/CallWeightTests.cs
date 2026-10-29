using Njord.Core.Polling;

namespace Njord.Core.Tests.Polling;

public sealed class CallWeightTests
{
    [Theory]
    [InlineData(10, 14, 1.0)]
    [InlineData(10, 4, 4 / 14.0)]
    [InlineData(15, 4, 2 * 4 / 14.0)]
    [InlineData(5, 92, 92 / 14.0)]
    [InlineData(11, 28, 4.0)]
    [InlineData(31, 14, 4.0)]
    public void Should_weigh_ceiling_of_ten_variables_times_fractional_fourteen_days(int variables, int days, double weight)
    {
        Assert.Equal(weight, CallWeight.Of(variables, days), 6);
    }
}
