using System.Text.Json.Nodes;
using Akka.Actor;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttStateDedupSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private readonly MqttSessionCounter _sessionCounter = new();

    private (FakePipelineOutput Pipeline, RecordingTransport Transport) Start()
    {
        var pipeline = new FakePipelineOutput(Sys);
        ActorRegistry.Register<IForecastPipeline>(pipeline.Actor, overwrite: true);
        var transport = new RecordingTransport();
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };

        var parameters = ParameterSelection.Resolve(["Weather"], [], []);
        var timeProvider = TestTimeouts.AtEpoch(Anchor);

        Sys.ActorOf(Props.Create(() => new MqttStatePublisher(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new MqttOptions { BaseTopic = "njord" }),
            parameters,
            timeProvider,
            transport,
            MqttEgressTuning.Default,
            _sessionCounter)), $"mqtt-state-{Guid.NewGuid():N}");
        return (pipeline, transport);
    }

    private static PipelineEvent.AlertRaised Frost(int peak) => new(
        new Location("lucerne", 47.05, 8.31),
        new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, null, 3), PeakValue: peak),
        Anchor);

    private static async Task WaitForFrostAsync(RecordingTransport transport, int peak, CancellationToken ct)
    {
        try
        {
            await transport.WaitForMessage(m => m.Topic == "njord/lucerne/alerts/frost" && PeakValue(m) == peak)
                .WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for the frost alert with peak_value={peak}; " +
                        $"published: {transport.Sent.Count}");
        }
    }

    private static double? PeakValue(SentMessage message) =>
        JsonNode.Parse(message.Payload)?["peak_value"]?.GetValue<double>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_payload_is_not_published_again_on_the_same_broker_connection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var subscription = await pipeline.NextSubscriptionAsync("the subscription", ct);

        await subscription.OfferAsync(Frost(1), ct);
        await subscription.OfferAsync(Frost(1), ct);
        await subscription.OfferAsync(Frost(2), ct);
        await WaitForFrostAsync(transport, 2, ct);

        Assert.Single(transport.Sent, m => PeakValue(m) == 1);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_payload_is_published_again_after_the_broker_connection_was_re_established()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var subscription = await pipeline.NextSubscriptionAsync("the subscription", ct);
        await subscription.OfferAsync(Frost(1), ct);
        await WaitForFrostAsync(transport, 1, ct);

        _sessionCounter.Increment();
        await subscription.OfferAsync(Frost(1), ct);

        await AwaitAssertAsync(
            () => Assert.Equal(2, transport.Sent.Count(m => PeakValue(m) == 1)),
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }
}
