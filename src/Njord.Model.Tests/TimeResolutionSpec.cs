namespace Njord.Model.Tests;

public sealed class TimeResolutionSpec
{
    [Theory]
    [InlineData(TimeResolution.QuarterHourly, 15)]
    [InlineData(TimeResolution.Hourly, 60)]
    [InlineData(TimeResolution.ThreeHourly, 180)]
    [InlineData(TimeResolution.SixHourly, 360)]
    [InlineData(TimeResolution.Daily, 1440)]
    public void To_time_span_returns_the_step_length(TimeResolution resolution, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), resolution.ToTimeSpan());
    }

    [Fact]
    public void Unknown_resolution_is_rejected()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((TimeResolution)99).ToTimeSpan());

        Assert.Equal("resolution", ex.ParamName);
    }
}
