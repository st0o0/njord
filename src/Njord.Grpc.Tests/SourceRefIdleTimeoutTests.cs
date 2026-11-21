using Akka;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
namespace Njord.Grpc.Tests;

public sealed class SourceRefIdleTimeoutTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly TimeSpan _keepAlive = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(1);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    [Fact(Timeout = 15_000)]
    public async Task Producer_side_idle_timeout_fires_during_demand_stall()
    {
        var ct = TestContext.Current.CancellationToken;
        var mat = Sys.Materializer();

        var streamTask = Source.Never<string>()
            .KeepAlive<string, string, NotUsed>(_keepAlive, () => "hb")
            .Buffer(16, OverflowStrategy.Backpressure)
            .IdleTimeout(_idleTimeout)
            .SelectAsync(1, async e =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
                return e;
            })
            .RunWith(Sink.Ignore<string>(), mat);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => streamTask.WaitAsync(TimeSpan.FromSeconds(10), ct));
        Assert.Contains("No elements passed", ex.GetBaseException().Message);
    }

    [Fact(Timeout = 15_000)]
    public async Task Consumer_side_idle_timeout_survives_upstream_silence()
    {
        var ct = TestContext.Current.CancellationToken;
        var mat = Sys.Materializer();

        var sourceRef = await Source.Never<string>()
            .KeepAlive<string, string, NotUsed>(_keepAlive, () => "hb")
            .RunWith(StreamRefs.SourceRef<string>(), mat);

        var streamTask = sourceRef.Source
            .IdleTimeout(_idleTimeout)
            .Where(static e => e != "hb")
            .Buffer(16, OverflowStrategy.Backpressure)
            .RunWith(Sink.Ignore<string>(), mat);

        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        Assert.False(streamTask.IsCompleted, "Stream should still be alive after 3x the idle timeout");
    }

    [Fact(Timeout = 15_000)]
    public async Task Consumer_side_idle_timeout_detects_silent_producer()
    {
        var ct = TestContext.Current.CancellationToken;
        var mat = Sys.Materializer();

        var sourceRef = await Source.Never<string>()
            .RunWith(StreamRefs.SourceRef<string>(), mat);

        var streamTask = sourceRef.Source
            .IdleTimeout(_idleTimeout)
            .Buffer(16, OverflowStrategy.Backpressure)
            .RunWith(Sink.Ignore<string>(), mat);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => streamTask.WaitAsync(TimeSpan.FromSeconds(5), ct));
        Assert.Contains("No elements passed", ex.GetBaseException().Message);
    }

    [Fact(Timeout = 15_000)]
    public async Task Consumer_side_keepalives_reset_timeout_under_backpressure()
    {
        var ct = TestContext.Current.CancellationToken;
        var mat = Sys.Materializer();

        var sourceRef = await Source.Never<string>()
            .KeepAlive<string, string, NotUsed>(_keepAlive, () => "hb")
            .RunWith(StreamRefs.SourceRef<string>(), mat);

        var streamTask = sourceRef.Source
            .IdleTimeout(_idleTimeout)
            .Where(static e => e != "hb")
            .Buffer(16, OverflowStrategy.Backpressure)
            .Throttle(1, TimeSpan.FromSeconds(2), 1, ThrottleMode.Shaping)
            .RunWith(Sink.Ignore<string>(), mat);

        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        Assert.False(streamTask.IsCompleted, "Stream should survive backpressure with consumer-side idle timeout");
    }
}
