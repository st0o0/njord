using System.Text.Json.Nodes;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

// A broker that stops answering must never hold up the pipeline output that other consumers read from.
public sealed class MqttBackpressureSpec : NjordActorSpec
{
    private const int EventCount = 200;
    private const int WarmUpPrecip = -1;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    protected override bool UseTestPersistence => false;

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_stuck_broker_does_not_hold_up_another_consumer_of_the_pipeline_output()
    {
        var ct = TestContext.Current.CancellationToken;
        var (queue, hubSource) = Source.Queue<PipelineEvent>(32, OverflowStrategy.Backpressure)
            .ToMaterialized(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Keep.Both)
            .Run(Sys.Materializer());
        ActorRegistry.Register<IForecastPipeline>(
            Sys.ActorOf(Props.Create(() => new HubPipeline(hubSource)), $"pipeline-{Guid.NewGuid():N}"), overwrite: true);
        var otherConsumer = CreateTestProbe();
        hubSource.RunWith(Sink.ActorRef<PipelineEvent>(otherConsumer.Ref, "done", ex => ex), Sys.Materializer());
        var transport = new StuckTransport();
        StartPublisher(transport);

        // The publisher is attached once its first publish hangs; the other consumer once it saw the warm-up.
        await AwaitAssertAsync(async () =>
        {
            await queue.OfferAsync(Frost(WarmUpPrecip), ct);
            await transport.FirstAttempt.WaitAsync(TimeSpan.FromMilliseconds(300), ct);
        }, TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        await otherConsumer.FishForMessageAsync<PipelineEvent.AlertRaised>(
            t => t.Alert.PeakValue == WarmUpPrecip, TestTimeouts.AwaitAssertMax, cancellationToken: ct);

        await OfferAllAsync(queue, ct);

        var received = new List<int>();
        while (received.Count < EventCount)
        {
            var frost = await otherConsumer.ExpectMsgAsync<PipelineEvent.AlertRaised>(TestTimeouts.AwaitAssertMax,
                cancellationToken: ct);
            if (frost.Alert.PeakValue is { } precip and > 0)
            {
                received.Add((int)precip);
            }
        }

        Assert.Equal(Enumerable.Range(1, EventCount), received);

        // Released, the publisher catches up with its most recent events only: the oldest were dropped.
        transport.Release();
        await transport.WaitForPrecip(EventCount).WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        Assert.InRange(transport.PublishedAlerts, 2, EventCount / 4);
    }

    private static async Task OfferAllAsync(ISourceQueueWithComplete<PipelineEvent> queue, CancellationToken ct)
    {
        var offered = 0;

        async Task OfferInOrder()
        {
            for (var precip = 1; precip <= EventCount; precip++)
            {
                Assert.IsType<QueueOfferResult.Enqueued>(await queue.OfferAsync(Frost(precip), ct));
                offered++;
            }
        }

        try
        {
            await OfferInOrder().WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The pipeline output stalled: only {Volatile.Read(ref offered)} of {EventCount} events " +
                        $"were accepted within {TestTimeouts.RealTimeWaitMax}");
        }
    }

    private void StartPublisher(IMqttTransport transport)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var parameters = ParameterSelection.Resolve(["Weather"], [], []);
        var timeProvider = TestTimeouts.AtEpoch(Anchor);
        var tuning = MqttEgressTuning.Default with { PublishTimeout = TimeSpan.FromHours(1), EventBufferSize = 4 };

        Sys.ActorOf(Props.Create(() => new MqttStatePublisher(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new MqttOptions { BaseTopic = "njord" }),
            parameters,
            timeProvider,
            transport,
            tuning,
            new MqttSessionCounter())), $"mqtt-state-{Guid.NewGuid():N}");
    }

    private static PipelineEvent.AlertRaised Frost(int peak) => new(
        new Location("lucerne", 47.05, 8.31),
        new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, null, 3), PeakValue: peak),
        Anchor);

    private sealed class HubPipeline : ReceiveActor
    {
        public HubPipeline(Source<PipelineEvent, Akka.NotUsed> hubSource)
        {
            var mat = Context.Materializer();
            Receive<RequestPipelineOutput>(msg =>
                hubSource.RunWith(StreamRefs.SourceRef<PipelineEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new RequestPipelineOutputCompleted(msg.RequestId, sr),
                        ex => new RequestPipelineOutputFailed(msg.RequestId, ex)));
        }
    }

    // Every publish waits until the spec releases the broker.
    private sealed class StuckTransport : IMqttTransport
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _gate = new();
        private readonly List<(int Precip, TaskCompletionSource Seen)> _waiters = [];
        private readonly List<int> _published = [];

        public Task FirstAttempt => _firstAttempt.Task;

        public int PublishedAlerts
        {
            get
            {
                lock (_gate)
                {
                    return _published.Count(p => p > 0);
                }
            }
        }

        public void Release() => _released.TrySetResult();

        public async Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            _firstAttempt.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
            if (topic != "njord/lucerne/alerts/frost" || JsonNode.Parse(payload)?["peak_value"]?.GetValue<double>() is not { } peakValue)
            {
                return;
            }

            lock (_gate)
            {
                _published.Add((int)peakValue);
                foreach (var waiter in _waiters.Where(w => w.Precip == (int)peakValue))
                {
                    waiter.Seen.TrySetResult();
                }
            }
        }

        public Task WaitForPrecip(int precip)
        {
            lock (_gate)
            {
                if (_published.Contains(precip))
                {
                    return Task.CompletedTask;
                }

                var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((precip, seen));
                return seen.Task;
            }
        }
    }
}
