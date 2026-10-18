using Njord.Mqtt.Actors;
using System.Text.Json.Nodes;
using Akka.Actor;
using Njord.Compute.Contracts;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Mqtt.Tests;

public sealed class MqttStateRewireSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private (FakePipelineOutput Pipeline, MqttStateSpec.RecordingTransport Transport) Start()
    {
        var pipeline = new FakePipelineOutput(Sys);
        ActorRegistry.Register<IForecastPipeline>(pipeline.Actor, overwrite: true);
        var transport = new MqttStateSpec.RecordingTransport();
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
            MqttEgressTuning.Default)), $"mqtt-state-{Guid.NewGuid():N}");
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

    private static async Task WaitForTrendAsync(MqttStateSpec.RecordingTransport transport, int precipStarts, CancellationToken ct)
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

    private static int? PrecipStarts(MqttStateSpec.SentMessage message) =>
        JsonNode.Parse(message.Payload)?["precip_starts"]?.GetValue<int>();

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Events_are_published_again_after_the_pipeline_subscription_failed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var first = await pipeline.NextSubscriptionAsync("the first subscription", ct);
        await first.OfferAsync(Trend(1), ct);
        await WaitForTrendAsync(transport, 1, ct);

        first.Fail(new InvalidOperationException("pipeline output failed"));

        var second = await pipeline.NextSubscriptionAsync("a new subscription after the failure", ct);
        await second.OfferAsync(Trend(2), ct);
        await WaitForTrendAsync(transport, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Events_are_published_again_after_the_pipeline_subscription_completed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var first = await pipeline.NextSubscriptionAsync("the first subscription", ct);
        await first.OfferAsync(Trend(1), ct);
        await WaitForTrendAsync(transport, 1, ct);

        first.Complete();

        var second = await pipeline.NextSubscriptionAsync("a new subscription after the completion", ct);
        await second.OfferAsync(Trend(2), ct);
        await WaitForTrendAsync(transport, 2, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Events_are_published_again_after_a_stage_stopped_on_an_unexpected_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var first = await pipeline.NextSubscriptionAsync("the first subscription", ct);

        // A forecast event without a forecast makes the mapping stage throw a NullReferenceException,
        // which the logging decider classifies as Stop.
        await first.OfferAsync(new PipelineEvent.ForecastReceived(new LocationId("lucerne"), new WeatherModel("icon_d2"), null!), ct);

        var second = await pipeline.NextSubscriptionAsync("a new subscription after the stopped stage", ct);
        await second.OfferAsync(Trend(3), ct);
        await WaitForTrendAsync(transport, 3, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_payload_is_published_again_after_a_rewire()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pipeline, transport) = Start();
        var first = await pipeline.NextSubscriptionAsync("the first subscription", ct);
        await first.OfferAsync(Trend(4), ct);
        await WaitForTrendAsync(transport, 4, ct);

        first.Fail(new InvalidOperationException("pipeline output failed"));
        var second = await pipeline.NextSubscriptionAsync("a new subscription after the failure", ct);
        await second.OfferAsync(Trend(4), ct);

        await AwaitAssertAsync(
            () => Assert.Equal(2, transport.Sent.Count(m => m.Topic == "njord/lucerne/trends")),
            TestTimeouts.AwaitAssertMax, cancellationToken: ct);
    }
}
