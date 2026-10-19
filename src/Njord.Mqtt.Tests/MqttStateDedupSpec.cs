using System.Text.Json.Nodes;
using Akka.Actor;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Messages.Trends;
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

    private static PipelineEvent.TrendComputed Trend(int precipStarts) => new(
        new TrendResult(
            new LocationId("lucerne"),
            new Dictionary<string, ParameterTrend?>(),
            null,
            new PrecipTimingInfo(precipStarts, precipStarts + 3),
            new ExtremaTimingInfo(null, null),
            null,
            null),
        Anchor);

    private static async Task WaitForTrendAsync(RecordingTransport transport, int precipStarts, CancellationToken ct)
    {
        try
        {
            await transport.WaitForMessage(m => m.Topic == "njord/lucerne/trends" && PrecipStarts(m) == precipStarts)
                .WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for the trend with precip_starts={precipStarts}; " +
                        $"published: {transport.Sent.Count}");
        }
    }

    private static int? PrecipStarts(SentMessage message) =>
        JsonNode.Parse(message.Payload)?["precip_starts"]?.GetValue<int>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_payload_is_not_published_again_on_the_same_broker_connection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var subscription = await pipeline.NextSubscriptionAsync("the subscription", ct);

        await subscription.OfferAsync(Trend(1), ct);
        await subscription.OfferAsync(Trend(1), ct);
        await subscription.OfferAsync(Trend(2), ct);
        await WaitForTrendAsync(transport, 2, ct);

        Assert.Single(transport.Sent, m => PrecipStarts(m) == 1);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_payload_is_published_again_after_the_broker_connection_was_re_established()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var subscription = await pipeline.NextSubscriptionAsync("the subscription", ct);
        await subscription.OfferAsync(Trend(1), ct);
        await WaitForTrendAsync(transport, 1, ct);

        _sessionCounter.Increment();
        await subscription.OfferAsync(Trend(1), ct);

        await AwaitAssertAsync(
            () => Assert.Equal(2, transport.Sent.Count(m => PrecipStarts(m) == 1)),
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }
}
