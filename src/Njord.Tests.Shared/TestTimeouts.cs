namespace Njord.Tests.Shared;

public static class TestTimeouts
{
    // Outer xUnit safety net for specs that start a hosted actor system. It must stay above the
    // TestKit waits it wraps (akka.test.timefactor = 3 dilates the 3 s defaults to 9 s), so the
    // descriptive Akka-level failure fires before xUnit kills the test.
    public const int Hosted = 30_000;

    // Explicit upper bound for AwaitAssert/AwaitCondition in hosted specs; below Hosted.
    public static readonly TimeSpan AwaitAssertMax = TimeSpan.FromSeconds(15);
}
