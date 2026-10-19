using Akka.Actor;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration.Mqtt;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public abstract class MqttConnectionSpecBase : NjordActorSpec
{
    internal const string AvailabilityTopic = "njord/status";

    protected static readonly DateTimeOffset Epoch = TestTimeConstants.PipelineEpoch;

    private static readonly MqttEgressTuning FastTuning =
        new MqttEgressTuning(TimeSpan.FromMilliseconds(50)) { PublishTimeout = TimeSpan.FromMilliseconds(200) };

    private readonly MqttSessionCounter _sessionCounter = new();

    protected override bool UseTestPersistence => false;

    protected FakeTimeProvider Time { get; } = TestTimeouts.AtEpoch(Epoch);

    internal IActorRef CreateActor(ScriptedMqttConnection connection, ScriptedAvailabilityTransport transport)
        => Sys.ActorOf(Props.Create(() => new MqttConnection(
                Microsoft.Extensions.Options.Options.Create(
                    new MqttOptions { Host = "localhost", BaseTopic = "njord", DiscoveryPrefix = "homeassistant" }),
                connection,
                transport,
                FastTuning,
                _sessionCounter,
                Time)),
            $"mqtt-connection-{Guid.NewGuid():N}");

    internal static Task WaitForOnline(ScriptedAvailabilityTransport transport, CancellationToken cancellationToken, int count = 1)
        => transport.WaitForSent(sent => sent.Count(m => m is { Topic: AvailabilityTopic, Payload: "online" }) >= count)
            .WaitAsync(TestTimeouts.RealTimeWaitMax, cancellationToken);
}
