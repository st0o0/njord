using System.Diagnostics;
using System.Diagnostics.Metrics;
using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.TestKit;
using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using Servus.Resilience;

namespace Njord.Core.Tests.Actors;

// The spec plays the pipeline itself: a probe registered as IForecastPipeline stands for the singleton proxy, and
// each reply is sent with an "incarnation" probe as sender, like the real pipeline child behind the proxy.
public sealed class StreamConsumerActorSpec : TestKit
{
    private static readonly BackoffPolicy FastPolicy =
        Backoff.Create(TimeSpan.FromMilliseconds(50), maxDelay: TimeSpan.FromMilliseconds(400));

    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        => builder.AddTestTimefactor();

    private sealed record Ping;

    private sealed record Pong;

    private sealed record Materialized(int Count);

    // Forwards every event to the reporter; its graph ends when the subscription ends.
    private sealed class TestConsumer : StreamConsumerActor
    {
        private readonly IActorRef _reporter;
        private readonly string _name;
        private int _materializations;

        public TestConsumer(IActorRef reporter, string name, BackoffPolicy policy, TimeSpan? stableAfter)
            : base(policy, ReplyTimeout, stableAfter)
        {
            _reporter = reporter;
            _name = name;
            Receive<Ping>(_ => Sender.Tell(new Pong()));
        }

        protected override string ConsumerName => _name;

        protected override Task MaterializeGraph(Source<PipelineEvent, NotUsed> events)
        {
            _materializations++;
            _reporter.Tell(new Materialized(_materializations));
            var reporter = _reporter;
            return events
                .WatchTermination(Keep.Right)
                .To(Sink.ForEach<PipelineEvent>(e => reporter.Tell(e)))
                .Run(Mat);
        }
    }

    private sealed record Subscription(ISourceQueueWithComplete<PipelineEvent> Queue, long RequestId);

    private readonly string _consumerName = $"consumer-{Guid.NewGuid():N}";

    private (IActorRef Consumer, TestProbe Reporter, TestProbe Proxy) Start(
        BackoffPolicy? policy = null, TimeSpan? stableAfter = null, bool register = true)
    {
        var proxy = CreateTestProbe();
        if (register)
        {
            ActorRegistry.Register<IForecastPipeline>(proxy, overwrite: true);
        }

        var reporter = CreateTestProbe();
        var name = _consumerName;
        var consumer = Sys.ActorOf(
            Props.Create(() => new TestConsumer(reporter.Ref, name, policy ?? FastPolicy, stableAfter)),
            $"consumer-{Guid.NewGuid():N}");
        return (consumer, reporter, proxy);
    }

    private async Task<RequestPipelineOutput> ExpectRequestAsync(TestProbe proxy, CancellationToken ct) =>
        await proxy.ExpectMsgAsync<RequestPipelineOutput>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);

    // Answers the next SourceRef request with a fresh queue-backed SourceRef. The SourceRef runs on the system
    // materializer, so it outlives the incarnation that "sent" it: only the consumer's own handling ends it.
    private async Task<Subscription> AnswerAsync(TestProbe proxy, IActorRef incarnation, CancellationToken ct)
    {
        var request = await ExpectRequestAsync(proxy, ct);
        var consumer = proxy.LastSender;
        return await ReplyAsync(consumer, request.RequestId, incarnation);
    }

    private async Task<Subscription> ReplyAsync(IActorRef consumer, long requestId, IActorRef incarnation)
    {
        var (queue, source) = Source.Queue<PipelineEvent>(16, OverflowStrategy.Backpressure).PreMaterialize(Sys.Materializer());
        var sourceRef = await source.RunWith(StreamRefs.SourceRef<PipelineEvent>(), Sys.Materializer());
        consumer.Tell(new RequestPipelineOutputCompleted(requestId, sourceRef), incarnation);
        return new Subscription(queue, requestId);
    }

    private static PipelineEvent Event(int hour) => EnrichmentEvents.Alerts(new LocationId("lucerne"), Anchor.AddHours(hour));

    private static async Task ExpectFlowAsync(Subscription subscription, TestProbe reporter, int hour, CancellationToken ct)
    {
        var expected = Event(hour);
        await subscription.Queue.OfferAsync(expected, ct);
        var received = await reporter.FishForMessageAsync(m => m is PipelineEvent, TestTimeouts.AwaitAssertMax,
            cancellationToken: ct);
        Assert.Equal(expected, received);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_consumer_attaches_on_start_and_consumes_its_subscription()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();

        var subscription = await AnswerAsync(proxy, CreateTestProbe(), ct);

        await reporter.ExpectMsgAsync<Materialized>(m => Assert.Equal(1, m.Count), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await ExpectFlowAsync(subscription, reporter, 1, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_consumer_answers_its_own_messages_before_the_first_source_ref()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, _, proxy) = Start();
        await ExpectRequestAsync(proxy, ct);

        var pong = await consumer.Ask<Pong>(new Ping(), TestTimeouts.RealTimeWaitMax, ct);

        Assert.Equal(new Pong(), pong);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_consumer_attaches_once_the_pipeline_is_registered_late()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, reporter, proxy) = Start(register: false);
        await reporter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(400), ct);

        ActorRegistry.Register<IForecastPipeline>(proxy, overwrite: true);
        var subscription = await AnswerAsync(proxy, CreateTestProbe(), ct);

        await reporter.ExpectMsgAsync<Materialized>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await ExpectFlowAsync(subscription, reporter, 1, ct);
        Assert.Equal(new Pong(), await consumer.Ask<Pong>(new Ping(), TestTimeouts.RealTimeWaitMax, ct));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unanswered_request_is_retried_with_a_new_request_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var first = await ExpectRequestAsync(proxy, ct);

        var second = await ExpectRequestAsync(proxy, ct);
        Assert.True(second.RequestId > first.RequestId,
            $"expected a new request id, got {first.RequestId} then {second.RequestId}");

        await ReplyAsync(proxy.LastSender, second.RequestId, CreateTestProbe());
        await reporter.ExpectMsgAsync<Materialized>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_request_is_retried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var first = await ExpectRequestAsync(proxy, ct);
        proxy.LastSender.Tell(new RequestPipelineOutputFailed(first.RequestId, new InvalidOperationException("no hub")), proxy.Ref);

        var subscription = await AnswerAsync(proxy, CreateTestProbe(), ct);

        await reporter.ExpectMsgAsync<Materialized>(m => Assert.Equal(1, m.Count), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await ExpectFlowAsync(subscription, reporter, 1, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stale_reply_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var first = await ExpectRequestAsync(proxy, ct);
        var second = await ExpectRequestAsync(proxy, ct);
        var consumer = proxy.LastSender;

        var stale = await ReplyAsync(consumer, first.RequestId, CreateTestProbe());
        var current = await ReplyAsync(consumer, second.RequestId, CreateTestProbe());

        await reporter.ExpectMsgAsync<Materialized>(m => Assert.Equal(1, m.Count), TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await stale.Queue.OfferAsync(Event(2), ct);
        await ExpectFlowAsync(current, reporter, 1, ct);
        await reporter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_consumer_re_attaches_when_the_pipeline_incarnation_terminates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var incarnation = CreateTestProbe();
        var old = await AnswerAsync(proxy, incarnation, ct);
        await ExpectFlowAsync(old, reporter, 1, ct);

        Sys.Stop(incarnation.Ref);

        var renewed = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(renewed, reporter, 2, ct);
        await old.Queue.WatchCompletionAsync().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_termination_of_an_earlier_incarnation_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var earlier = CreateTestProbe();
        var first = await AnswerAsync(proxy, earlier, ct);
        first.Queue.Fail(new InvalidOperationException("stage stopped"));
        var current = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(current, reporter, 1, ct);

        Sys.Stop(earlier.Ref);

        await proxy.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(400), ct);
        await ExpectFlowAsync(current, reporter, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_graph_is_re_attached_with_a_new_source_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var first = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(first, reporter, 1, ct);

        first.Queue.Fail(new InvalidOperationException("stage stopped"));

        var second = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(second, reporter, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_completed_graph_is_re_attached_with_a_new_source_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var first = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(first, reporter, 1, ct);

        first.Queue.Complete();

        var second = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(second, reporter, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_completion_of_a_graph_replaced_after_a_pipeline_termination_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, reporter, proxy) = Start();
        var incarnation = CreateTestProbe();
        var replaced = await AnswerAsync(proxy, incarnation, ct);
        Sys.Stop(incarnation.Ref);
        var current = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(current, reporter, 1, ct);

        replaced.Queue.Complete();

        await proxy.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(400), ct);
        current.Queue.Fail(new InvalidOperationException("stage stopped"));
        var third = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(third, reporter, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stopped_consumer_does_not_re_attach()
    {
        var ct = TestContext.Current.CancellationToken;
        var (consumer, _, proxy) = Start();
        var first = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await WatchAsync(consumer);

        Sys.Stop(consumer);
        await ExpectTerminatedAsync(consumer, cancellationToken: ct);

        await first.Queue.WatchCompletionAsync().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        await proxy.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(400), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_re_attach_delay_grows_when_every_new_graph_fails_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var policy = Backoff.Create(TimeSpan.FromMilliseconds(200), maxDelay: TimeSpan.FromSeconds(5));
        var (_, _, proxy) = Start(policy);
        var subscription = await AnswerAsync(proxy, CreateTestProbe(), ct);
        var gaps = new List<TimeSpan>();

        for (var i = 0; i < 4; i++)
        {
            var clock = Stopwatch.StartNew();
            subscription.Queue.Fail(new InvalidOperationException("stage stopped"));
            subscription = await AnswerAsync(proxy, CreateTestProbe(), ct);
            gaps.Add(clock.Elapsed);
        }

        // Delays are 200, 400, 800 and 1600 ms, each with +-25 % jitter.
        Assert.True(gaps[3] > gaps[0] * 3,
            $"expected the fourth delay to exceed three times the first; gaps: {string.Join(", ", gaps.Select(g => g.TotalMilliseconds))} ms");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_re_attach_delay_starts_over_after_a_subscription_stayed_up_for_the_stability_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var policy = Backoff.Create(TimeSpan.FromMilliseconds(300), maxDelay: TimeSpan.FromSeconds(5));
        var (_, reporter, proxy) = Start(policy, stableAfter: TimeSpan.FromMilliseconds(500));
        var first = await AnswerAsync(proxy, CreateTestProbe(), ct);
        first.Queue.Fail(new InvalidOperationException("stage stopped"));
        var second = await AnswerAsync(proxy, CreateTestProbe(), ct);
        second.Queue.Fail(new InvalidOperationException("stage stopped"));
        var stable = await AnswerAsync(proxy, CreateTestProbe(), ct);

        // Dilated to 900 ms of real time, beyond the 500 ms stability window.
        await reporter.FishForMessageAsync(m => m is Materialized { Count: 3 }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await reporter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        var clock = Stopwatch.StartNew();
        stable.Queue.Fail(new InvalidOperationException("stage stopped"));
        await AnswerAsync(proxy, CreateTestProbe(), ct);

        // Without the reset this third loss would wait 1200 ms +-25 %; after it, 300 ms +-25 %.
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(700),
            $"expected the delay to start over at the initial delay, took {clock.Elapsed.TotalMilliseconds} ms");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_lost_subscription_is_counted_once_with_the_consumer_name()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rewires = new RewireCounter(_consumerName);
        var (_, reporter, proxy) = Start();
        var incarnation = CreateTestProbe();
        var first = await AnswerAsync(proxy, incarnation, ct);
        await ExpectFlowAsync(first, reporter, 1, ct);

        // Both signals of a pipeline restart: the incarnation terminates and its SourceRef fails.
        Sys.Stop(incarnation.Ref);
        first.Queue.Fail(new InvalidOperationException("pipeline gone"));

        var second = await AnswerAsync(proxy, CreateTestProbe(), ct);
        await ExpectFlowAsync(second, reporter, 2, ct);
        await proxy.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        Assert.Equal(1, rewires.Value);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_first_attach_and_failed_attempts_are_not_counted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rewires = new RewireCounter(_consumerName);
        var (_, reporter, proxy) = Start();
        await ExpectRequestAsync(proxy, ct);

        var subscription = await AnswerAsync(proxy, CreateTestProbe(), ct);

        await ExpectFlowAsync(subscription, reporter, 1, ct);
        Assert.Equal(0, rewires.Value);
    }

    [Fact]
    public void The_default_retry_policy_caps_at_thirty_seconds()
    {
        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromSeconds(1), StreamConsumerActor.DefaultRetryPolicy.Delay(0)),
            () => Assert.Equal(TimeSpan.FromSeconds(30), StreamConsumerActor.DefaultRetryPolicy.Delay(10)));
    }

    private sealed class RewireCounter : IDisposable
    {
        private readonly MeterListener _listener = new();
        private long _value;

        public RewireCounter(string consumer)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "Njord.Core" && instrument.Name == "njord_stream_consumer_rewires_total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "consumer" && Equals(tag.Value, consumer))
                    {
                        Interlocked.Add(ref _value, value);
                    }
                }
            });
            _listener.Start();
        }

        public long Value => Interlocked.Read(ref _value);

        public void Dispose() => _listener.Dispose();
    }
}
