using System.Diagnostics;

namespace Njord.Pipeline.Tests;

public sealed class StopwatchGapSpec
{
    [Fact]
    public void Timestamps_one_hundredth_of_a_second_apart_are_ten_milliseconds()
    {
        var previous = Stopwatch.GetTimestamp();
        var next = previous + (Stopwatch.Frequency / 100);

        var gapMs = StopwatchGap.Milliseconds(previous, next);

        Assert.Equal(10.0, gapMs, precision: 3);
    }
}
