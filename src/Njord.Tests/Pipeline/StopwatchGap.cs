using System.Diagnostics;

namespace Njord.Tests.Pipeline;

internal static class StopwatchGap
{
    public static double Milliseconds(long previous, long next) =>
        Stopwatch.GetElapsedTime(previous, next).TotalMilliseconds;
}
