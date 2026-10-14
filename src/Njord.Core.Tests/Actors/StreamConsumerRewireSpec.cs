using System.Diagnostics;
using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.TestKit;
using Njord.Core.Actors;
using Njord.Tests.Shared;
using Servus.Akka;
using Servus.Resilience;

namespace Njord.Core.Tests.Actors;

public sealed class StreamConsumerRewireSpec : TestKit
{
    private static readonly BackoffPolicy FastPolicy =
        Backoff.Create(TimeSpan.FromMilliseconds(50), maxDelay: TimeSpan.FromMilliseconds(400));

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        => builder.AddTestTimefactor();

    private sealed class DepKey;

    private sealed record DepResolved(IActorRef Ref);

    private sealed record SourceRequested(int Count);

    private sealed record Materialized(int Count, TaskCompletionSource Graph);

    // Every SourceRef request succeeds at once; every materialized graph is a TaskCompletionSource the spec ends.
    private sealed class RewireConsumer : StreamConsumerActor
    {
        private readonly IActorRef _reporter;
        private IActorRef? _dep;
        private bool _hasRef;
        private int _requests;
        private int _materializations;

        public RewireConsumer(IActorRef reporter, BackoffPolicy retryPolicy, TimeSpan? graphStableAfter = null)
            : base(retryPolicy, graphStableAfter)
        {
            _reporter = reporter;
        }

        protected override void ResolveInitialDependencies()
        {
            _dep = Context.GetActor<DepKey>();
            TrackDependency(_dep);
        }

        protected override void RequestSourceRefs()
        {
            _requests++;
            _reporter.Tell(new SourceRequested(_requests));
            _hasRef = true;
            TryTransition();
        }

        protected override void ResolveDependencies()
        {
            Context.GetActorAsync<DepKey>().PipeTo(Self, success: r => new DepResolved(r));
        }

        protected override void ConfigureWaitingForRefs()
        {
            Receive<DepResolved>(msg =>
            {
                if (IsDeadRef(msg.Ref))
                {
                    ScheduleRetryResolve();
                    return;
                }

                TrackDependency(msg.Ref);
                _dep = msg.Ref;
                RequestSourceRefs();
            });
        }

        protected override bool AllRefsReady() => _dep is not null && _hasRef;

        protected override Task MaterializeGraph(Akka.Streams.SharedKillSwitch killSwitch)
        {
            _materializations++;
            var graph = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _reporter.Tell(new Materialized(_materializations, graph));
            return graph.Task;
        }

        protected override void OnStreamLost()
        {
            _hasRef = false;
        }

        protected override void OnDependencyLost()
        {
            _dep = null;
            _hasRef = false;
        }
    }

    private (IActorRef Consumer, TestProbe Reporter) Start(BackoffPolicy? policy = null, TimeSpan? graphStableAfter = null)
    {
        ActorRegistry.Register<DepKey>(CreateTestProbe(), overwrite: true);
        var reporter = CreateTestProbe();
        var consumer = Sys.ActorOf(
            Props.Create(() => new RewireConsumer(reporter.Ref, policy ?? FastPolicy, graphStableAfter)),
            $"consumer-{Guid.NewGuid():N}");
        return (consumer, reporter);
    }

    private static async Task<Materialized> ExpectMaterializedAsync(TestProbe reporter, int count, CancellationToken ct)
    {
        await reporter.ExpectMsgAsync<SourceRequested>(m => Assert.Equal(count, m.Count), TestTimeouts.AwaitAssertMax,
            cancellationToken: ct);
        var materialized = await reporter.ExpectMsgAsync<Materialized>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(count, materialized.Count);
        return materialized;
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_graph_is_rewired_with_a_new_source_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter) = Start();
        var first = await ExpectMaterializedAsync(reporter, 1, ct);

        first.Graph.SetException(new InvalidOperationException("stage stopped"));

        await ExpectMaterializedAsync(reporter, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_completed_graph_is_rewired_with_a_new_source_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter) = Start();
        var first = await ExpectMaterializedAsync(reporter, 1, ct);

        first.Graph.SetResult();

        await ExpectMaterializedAsync(reporter, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_completion_of_a_graph_replaced_after_a_dependency_loss_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var oldDep = CreateTestProbe();
        ActorRegistry.Register<DepKey>(oldDep, overwrite: true);
        var reporter = CreateTestProbe();
        Sys.ActorOf(Props.Create(() => new RewireConsumer(reporter.Ref, FastPolicy)), $"consumer-{Guid.NewGuid():N}");
        var replaced = await ExpectMaterializedAsync(reporter, 1, ct);
        ActorRegistry.Register<DepKey>(CreateTestProbe(), overwrite: true);
        await oldDep.Ref.GracefulStop(TimeSpan.FromSeconds(2));
        var current = await ExpectMaterializedAsync(reporter, 2, ct);

        replaced.Graph.SetResult();

        await reporter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        current.Graph.SetException(new InvalidOperationException("stage stopped"));
        await ExpectMaterializedAsync(reporter, 3, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stopped_consumer_does_not_rewire()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, reporter) = Start();
        var first = await ExpectMaterializedAsync(reporter, 1, ct);
        await WatchAsync(consumer);

        Sys.Stop(consumer);
        await ExpectTerminatedAsync(consumer, cancellationToken: ct);
        first.Graph.SetResult();

        await reporter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_rewire_delay_grows_when_every_new_graph_fails_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var policy = Backoff.Create(TimeSpan.FromMilliseconds(200), maxDelay: TimeSpan.FromSeconds(5));
        var (_, reporter) = Start(policy);
        var graph = await ExpectMaterializedAsync(reporter, 1, ct);
        var gaps = new List<TimeSpan>();

        for (var count = 2; count <= 5; count++)
        {
            var clock = Stopwatch.StartNew();
            graph.Graph.SetException(new InvalidOperationException("stage stopped"));
            graph = await ExpectMaterializedAsync(reporter, count, ct);
            gaps.Add(clock.Elapsed);
        }

        // Delays are 200, 400, 800 and 1600 ms, each with +-25 % jitter.
        Assert.True(gaps[3] > gaps[0] * 3,
            $"expected the fourth rewire delay to exceed three times the first; gaps: {string.Join(", ", gaps.Select(g => g.TotalMilliseconds))} ms");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_rewire_delay_starts_over_after_a_graph_stayed_up_for_the_stability_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var policy = Backoff.Create(TimeSpan.FromMilliseconds(300), maxDelay: TimeSpan.FromSeconds(5));
        var (_, reporter) = Start(policy, graphStableAfter: TimeSpan.FromMilliseconds(500));
        var first = await ExpectMaterializedAsync(reporter, 1, ct);
        first.Graph.SetException(new InvalidOperationException("stage stopped"));
        var second = await ExpectMaterializedAsync(reporter, 2, ct);
        second.Graph.SetException(new InvalidOperationException("stage stopped"));
        var stable = await ExpectMaterializedAsync(reporter, 3, ct);

        // Dilated to 900 ms of real time, beyond the 500 ms stability window.
        await reporter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        var clock = Stopwatch.StartNew();
        stable.Graph.SetException(new InvalidOperationException("stage stopped"));
        await ExpectMaterializedAsync(reporter, 4, ct);

        // Without the reset this third loss would wait 1200 ms +-25 %; after it, 300 ms +-25 %.
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(700),
            $"expected the delay to start over at the initial delay, took {clock.Elapsed.TotalMilliseconds} ms");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_rewire_is_counted_with_the_consumer_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var rewires = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Njord.Core" && instrument.Name == "njord_stream_consumer_rewires_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "consumer" && Equals(tag.Value, nameof(RewireConsumer)))
                {
                    Interlocked.Add(ref rewires, value);
                }
            }
        });
        listener.Start();
        var (_, reporter) = Start();
        var first = await ExpectMaterializedAsync(reporter, 1, ct);

        first.Graph.SetException(new InvalidOperationException("stage stopped"));

        await ExpectMaterializedAsync(reporter, 2, ct);
        await AwaitAssertAsync(() => Assert.Equal(1, Interlocked.Read(ref rewires)), TestTimeouts.AwaitAssertMax,
            cancellationToken: ct);
    }
}
