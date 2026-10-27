namespace Njord.Core.Tests;

public sealed class TimeResolutionTests
{
    [Theory]
    [InlineData(TimeResolution.Hourly, 60)]
    [InlineData(TimeResolution.Daily, 1440)]
    public void Should_convert_resolution_to_time_span(TimeResolution resolution, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), resolution.ToTimeSpan());
    }

    [Fact]
    public void Should_throw_for_undefined_resolution()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((TimeResolution)99).ToTimeSpan());

        Assert.Equal("resolution", ex.ParamName);
    }
}
