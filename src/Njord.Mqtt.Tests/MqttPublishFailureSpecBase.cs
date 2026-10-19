using Akka.Actor;
using Akka.Streams;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public abstract class MqttPublishFailureSpecBase : NjordActorSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private static readonly MqttEgressTuning FastTimeout =
        MqttEgressTuning.Default with { PublishTimeout = TimeSpan.FromMilliseconds(200) };

    protected override bool UseTestPersistence => false;

    private IActorRef CreatePublisher(IMqttTransport transport)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };

        var parameters = ParameterSelection.Resolve(["Weather"], [], []);
        var timeProvider = TestTimeouts.AtEpoch(Anchor);

        return Sys.ActorOf(Props.Create(() => new MqttStatePublisher(
            Microsoft.Extensions.Options.Options.Create(new MqttOptions { BaseTopic = "njord" }),
            parameters,
            timeProvider,
            transport,
            FastTimeout,
            new MqttSessionCounter())), $"mqtt-state-{Guid.NewGuid():N}");
    }

    private protected async Task<MqttStateSpec.FakeSourceHub> StartPublisherAsync(IMqttTransport transport)
    {
        var hub = new MqttStateSpec.FakeSourceHub(Sys.Materializer());
        ActorRegistry.Register<IForecastPipeline>(hub.Actor(Sys, typeof(RequestPipelineOutput)), overwrite: true);
        CreatePublisher(transport);
        await hub.WaitForQueue().WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        return hub;
    }

    protected static PipelineEvent.AlertRaised Frost(string location, int peak) => new(
        new Location(location, 47.05, 8.31),
        new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, null, 3), PeakValue: peak),
        Anchor);
}
