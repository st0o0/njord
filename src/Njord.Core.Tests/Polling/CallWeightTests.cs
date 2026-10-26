using Njord.Core.Polling;

namespace Njord.Core.Tests.Polling;

public sealed class CallWeightTests
{
    [Theory]
    [InlineData(10, 4, 1)]
    [InlineData(11, 4, 2)]
    [InlineData(31, 4, 4)]
    [InlineData(10, 15, 2)]
    [InlineData(11, 30, 6)]
    public void Should_weigh_one_call_per_ten_variables_and_fourteen_days(int variables, int days, int weight)
    {
        Assert.Equal(weight, CallWeight.Of(variables, days));
    }
}
