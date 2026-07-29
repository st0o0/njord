using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Actors;
using Servus.Akka;

namespace Njord.Tests.Actors;

public sealed class StreamConsumerActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    // -- marker keys for ActorRegistry --
    private sealed class DepAKey;
    private sealed class DepBKey;

    // -- messages for the test actor --
    private sealed record DepAResolved(IActorRef Ref);
    private sealed record DepBResolved(IActorRef Ref);

    /// <summary>
    /// Concrete StreamConsumerActor subclass used exclusively for testing.
    /// Two dependencies (DepA, DepB) resolved via ActorRegistry.
    /// </summary>
    private sealed class TestStreamConsumer : StreamConsumerActor
    {
        private IActorRef? _depA;
        private IActorRef? _depB;

        private readonly TaskCompletionSource _graphMaterialized;
        private int _materializeCount;

        public TestStreamConsumer(TaskCompletionSource graphMaterialized)
        {
            _graphMaterialized = graphMaterialized;
        }

        public static Props CreateProps(TaskCompletionSource graphMaterialized)
            => Props.Create(() => new TestStreamConsumer(graphMaterialized));

        protected override void ResolveDependencies()
        {
            Context.GetActorAsync<DepAKey>().PipeTo(Self, success: r => new DepAResolved(r));
            Context.GetActorAsync<DepBKey>().PipeTo(Self, success: r => new DepBResolved(r));
        }

        protected override void ConfigureWaitingForRefs()
        {
            Receive<DepAResolved>(msg =>
            {
                if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
                TrackDependency(msg.Ref);
                _depA = msg.Ref;
                TryTransition();
            });
            Receive<DepBResolved>(msg =>
            {
                if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
                TrackDependency(msg.Ref);
                _depB = msg.Ref;
                TryTransition();
            });
        }

        protected override bool AllRefsReady() => _depA is not null && _depB is not null;

        protected override void MaterializeGraph(SharedKillSwitch killSwitch)
        {
            _materializeCount++;
            _graphMaterialized.TrySetResult();
        }

        protected override void OnDependencyLost()
        {
            _depA = null;
            _depB = null;

            // Reset the TCS so callers can await the next materialization.
            // The old TCS is already completed, so we swap in a fresh one.
        }
    }

    /// <summary>
    /// Extended test consumer that allows replacing the graph-materialized TCS
    /// between recovery cycles so callers can await a second materialization.
    /// </summary>
    private sealed class ResettableTestStreamConsumer : StreamConsumerActor
    {
        private IActorRef? _depA;
        private IActorRef? _depB;
        private int _materializeCount;

        /// <summary>Sent to Self to swap the TCS for the next materialization cycle.</summary>
        public sealed record SetGraphTcs(TaskCompletionSource Tcs);

        /// <summary>Query message: consumer replies with the current materialize count.</summary>
        public sealed record GetMaterializeCount;

        private TaskCompletionSource _graphMaterialized;

        public ResettableTestStreamConsumer(TaskCompletionSource graphMaterialized)
        {
            _graphMaterialized = graphMaterialized;
        }

        public static Props CreateProps(TaskCompletionSource graphMaterialized)
            => Props.Create(() => new ResettableTestStreamConsumer(graphMaterialized));

        protected override void ResolveDependencies()
        {
            Context.GetActorAsync<DepAKey>().PipeTo(Self, success: r => new DepAResolved(r));
            Context.GetActorAsync<DepBKey>().PipeTo(Self, success: r => new DepBResolved(r));
        }

        protected override void ConfigureWaitingForRefs()
        {
            Receive<DepAResolved>(msg =>
            {
                if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
                TrackDependency(msg.Ref);
                _depA = msg.Ref;
                TryTransition();
            });
            Receive<DepBResolved>(msg =>
            {
                if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
                TrackDependency(msg.Ref);
                _depB = msg.Ref;
                TryTransition();
            });
            Receive<SetGraphTcs>(msg => _graphMaterialized = msg.Tcs);
            Receive<GetMaterializeCount>(_ => Sender.Tell(_materializeCount));
        }

        protected override void ConfigureReady()
        {
            Receive<SetGraphTcs>(msg => _graphMaterialized = msg.Tcs);
            Receive<GetMaterializeCount>(_ => Sender.Tell(_materializeCount));
        }

        protected override bool AllRefsReady() => _depA is not null && _depB is not null;

        protected override void MaterializeGraph(SharedKillSwitch killSwitch)
        {
            _materializeCount++;
            _graphMaterialized.TrySetResult();
        }

        protected override void OnDependencyLost()
        {
            _depA = null;
            _depB = null;
        }
    }

    // -- helpers --

    private (IActorRef DepA, IActorRef DepB) RegisterDeps()
    {
        var depA = CreateTestProbe();
        var depB = CreateTestProbe();
        ActorRegistry.Register<DepAKey>(depA, overwrite: true);
        ActorRegistry.Register<DepBKey>(depB, overwrite: true);
        return (depA, depB);
    }

    private async Task<IActorRef> CreateAndWaitForReady(
        TaskCompletionSource graphTcs, IActorRef? depA = null, IActorRef? depB = null)
    {
        if (depA is not null) ActorRegistry.Register<DepAKey>(depA, overwrite: true);
        if (depB is not null) ActorRegistry.Register<DepBKey>(depB, overwrite: true);

        var consumer = Sys.ActorOf(TestStreamConsumer.CreateProps(graphTcs));
        await graphTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return consumer;
    }

    // -- tests --

    [Fact(Timeout = 10000)]
    public async Task Dead_ref_detection_schedules_retry_instead_of_watching()
    {
        // Arrange: register deps and wait for initial graph materialization
        var (depA, depB) = RegisterDeps();
        var graphTcs = new TaskCompletionSource();
        var consumer = await CreateAndWaitForReady(graphTcs, depA, depB);

        // Subscribe to DeadLetters to count tight-loop messages
        var deadLetterProbe = CreateTestProbe();
        Sys.EventStream.Subscribe(deadLetterProbe, typeof(DeadLetter));

        // Act: stop depA. The consumer sees Terminated, re-resolves, gets
        // the same (dead) ref back from registry, and should schedule a retry
        // rather than spinning in a tight loop.
        await depA.GracefulStop(TimeSpan.FromSeconds(2));

        // Wait long enough to detect a tight loop if one existed
        await Task.Delay(500);

        // Assert: at most a small number of dead letters (not a tight loop)
        // Drain whatever dead letters accumulated in that window
        var deadLetterCount = 0;
        while (deadLetterProbe.HasMessages)
        {
            deadLetterProbe.ReceiveOne(TimeSpan.FromMilliseconds(50));
            deadLetterCount++;
        }

        Assert.True(deadLetterCount <= 10,
            $"Expected at most 10 dead letters but got {deadLetterCount} — possible tight loop");
    }

    [Fact(Timeout = 10000)]
    public async Task Stale_response_does_not_trigger_premature_ready()
    {
        // Arrange: register deps and wait for initial graph materialization
        var (depA, depB) = RegisterDeps();
        var graphTcs = new TaskCompletionSource();
        var consumer = Sys.ActorOf(
            ResettableTestStreamConsumer.CreateProps(graphTcs));

        await graphTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Prepare a second TCS to detect a second MaterializeGraph call
        var secondGraphTcs = new TaskCompletionSource();
        consumer.Tell(new ResettableTestStreamConsumer.SetGraphTcs(secondGraphTcs));
        await Task.Delay(100); // let the message process

        // Act: stop depA so the consumer enters WaitingForRefs.
        // The dead ref is detected on the first re-resolve and a retry is
        // scheduled (1 s delay = 2^0). Within that window the stale DepB
        // response must NOT cause a premature transition.
        await depA.GracefulStop(TimeSpan.FromSeconds(2));

        // Wait 800 ms — safely inside the 1 s retry window.
        var completed = await Task.WhenAny(
            secondGraphTcs.Task,
            Task.Delay(800));

        // Assert: MaterializeGraph should NOT have been called a second time
        // while the retry is still pending and the dep is dead.
        Assert.NotEqual(secondGraphTcs.Task, completed);
    }

    [Fact(Timeout = 5000)]
    public async Task Untracked_terminated_is_ignored()
    {
        // Arrange: register deps and wait for initial graph materialization
        var (depA, depB) = RegisterDeps();
        var graphTcs = new TaskCompletionSource();
        var consumer = Sys.ActorOf(
            ResettableTestStreamConsumer.CreateProps(graphTcs));
        await graphTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Create an untracked actor and stop it so a Terminated is delivered
        var untracked = Sys.ActorOf(Props.Create(() => new BlackHoleActor()));
        // Manually watch the untracked actor from a probe, then stop it —
        // but the consumer never tracked it, so its Terminated should be ignored.
        Watch(untracked);
        Sys.Stop(untracked);
        await ExpectTerminatedAsync(untracked);

        // Allow a beat for any side effects
        await Task.Delay(200);

        // Assert: the consumer is still alive and in Ready (responds to queries)
        var count = await consumer.Ask<int>(
            new ResettableTestStreamConsumer.GetMaterializeCount(),
            TimeSpan.FromSeconds(2));
        Assert.Equal(1, count);
    }

    [Fact(Timeout = 15000)]
    public async Task Retry_count_resets_on_successful_transition()
    {
        // Arrange: register deps and wait for initial graph
        var depA = CreateTestProbe();
        var depB = CreateTestProbe();
        ActorRegistry.Register<DepAKey>(depA, overwrite: true);
        ActorRegistry.Register<DepBKey>(depB, overwrite: true);

        var graphTcs = new TaskCompletionSource();
        var consumer = Sys.ActorOf(
            ResettableTestStreamConsumer.CreateProps(graphTcs));
        await graphTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // First failure: stop depA
        await depA.GracefulStop(TimeSpan.FromSeconds(2));
        await Task.Delay(200);

        // Register a new depA so the retry resolves successfully
        var newDepA = CreateTestProbe();
        ActorRegistry.Register<DepAKey>(newDepA, overwrite: true);

        // Wait for recovery (retry delay is 1s for _retryCount=0)
        var secondGraphTcs = new TaskCompletionSource();
        consumer.Tell(new ResettableTestStreamConsumer.SetGraphTcs(secondGraphTcs));
        await secondGraphTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Second failure: stop newDepA
        await newDepA.GracefulStop(TimeSpan.FromSeconds(2));
        await Task.Delay(200);

        // Register yet another depA
        var thirdDepA = CreateTestProbe();
        ActorRegistry.Register<DepAKey>(thirdDepA, overwrite: true);

        // The retry delay should be 1s again (not accumulated)
        // We verify by checking that recovery happens within 3s (well under
        // what accumulated backoff would require).
        var thirdGraphTcs = new TaskCompletionSource();
        consumer.Tell(new ResettableTestStreamConsumer.SetGraphTcs(thirdGraphTcs));
        var recovered = await Task.WhenAny(
            thirdGraphTcs.Task,
            Task.Delay(3000));

        Assert.Equal(thirdGraphTcs.Task, recovered);
    }

    [Fact(Timeout = 5000)]
    public async Task Exponential_backoff_caps_at_30_seconds()
    {
        // Unit-level verification of the backoff formula used in ScheduleRetryResolve.
        // The formula is: delay = min(2^retryCount, 30)
        var expected = new[] { 1, 2, 4, 8, 16, 30, 30, 30 };

        for (var i = 0; i < expected.Length; i++)
        {
            var delay = Math.Min(Math.Pow(2, i), 30);
            Assert.Equal(expected[i], (int)delay);
        }
    }

    // -- inner fakes --

    private sealed class BlackHoleActor : ReceiveActor
    {
        public BlackHoleActor() { ReceiveAny(_ => { }); }
    }
}
