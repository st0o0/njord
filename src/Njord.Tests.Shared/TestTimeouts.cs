using Microsoft.Extensions.Time.Testing;

namespace Njord.Tests.Shared;

public static class TestTimeouts
{
    // akka.test.timefactor for every TestKit spec (AddTestTimefactor). TestKit dilates its defaults
    // and every explicit duration passed to ExpectMsg/ExpectNoMsg/AwaitAssert/AwaitCondition by it.
    public const int TimeFactor = 3;

    // Outer xUnit safety net for specs that start a hosted actor system. It must stay above every
    // dilated TestKit wait it wraps, so the descriptive Akka-level failure fires before xUnit kills
    // the test. TestTimeoutsSpec enforces the margin.
    public const int Hosted = 30_000;

    // Un-dilated bound to pass to TestKit waits (AwaitAssert/AwaitCondition/ExpectMsg) in hosted
    // specs; TestKit stretches it to AwaitAssertMax * TimeFactor (15 s).
    public static readonly TimeSpan AwaitAssertMax = TimeSpan.FromSeconds(5);

    // Bound for waits TestKit does not dilate (Task.WaitAsync, manual deadlines): the same 15 s.
    public static readonly TimeSpan RealTimeWaitMax = AwaitAssertMax * TimeFactor;

    public static FakeTimeProvider AtEpoch(DateTimeOffset? offset = null)
        => new(offset ?? TestTimeConstants.PipelineEpoch);
}
