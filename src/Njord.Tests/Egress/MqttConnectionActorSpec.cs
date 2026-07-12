using System.Collections.Concurrent;
using Akka.Actor;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class MqttConnectionActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("mqtt-actor-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions Options(params string[] models) => new()
    {
        Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
        Models = [.. models],
        Horizons = [3, 24],
        Mqtt = new MqttOptions { Host = "broker.local" },
    };

    private static readonly ResolvedParameterSet TestParameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private IActorRef CreateActor(NjordOptions options, FakePublisher publisher)
        => _system.ActorOf(Props.Create(() => new MqttConnectionActor(
            Microsoft.Extensions.Options.Options.Create(options),
            publisher,
            NullLogger<MqttConnectionActor>.Instance,
            new MqttEgressTuning(TimeSpan.FromMilliseconds(50)),
            TestParameters)));

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"timed out waiting: {because}");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task Connecting_announces_online_and_subscribes_to_ha_status()
    {
        var publisher = new FakePublisher();
        CreateActor(Options("icon_d2"), publisher);

        await WaitUntilAsync(
            () => publisher.Published.Any(p => p is ("njord/status", "online", true)),
            "retained online announcement");
        await WaitUntilAsync(
            () => publisher.Subscriptions.Contains("homeassistant/status"),
            "HA status subscription");
    }

    [Fact(Timeout = 5000)]
    public async Task Discovery_is_published_retained_for_every_location_model_pair()
    {
        var publisher = new FakePublisher();
        CreateActor(Options("icon_d2", "gfs_seamless"), publisher);

        await WaitUntilAsync(() => ConfigPublishes(publisher).Count == 2, "two device configs");
        Assert.All(ConfigPublishes(publisher), p =>
        {
            Assert.True(p.Retain);
            Assert.Contains("cmps", p.Payload);
        });
        Assert.Contains(ConfigPublishes(publisher), p => p.Topic == "homeassistant/device/njord_home_icon_d2/config");
    }

    [Fact(Timeout = 5000)]
    public async Task Ha_birth_triggers_rediscovery()
    {
        var publisher = new FakePublisher();
        CreateActor(Options("icon_d2"), publisher);
        await WaitUntilAsync(() => ConfigPublishes(publisher).Count == 1, "initial discovery");

        publisher.SimulateInbound("homeassistant/status", "online");

        await WaitUntilAsync(() => ConfigPublishes(publisher).Count == 2, "re-discovery after birth");
    }

    [Fact(Timeout = 5000)]
    public async Task Cycle_results_publish_retained_state_per_received_forecast()
    {
        var publisher = new FakePublisher();
        var actor = CreateActor(Options("icon_d2", "gfs_seamless"), publisher);
        await WaitUntilAsync(() => ConfigPublishes(publisher).Count == 2, "discovery done");

        var cycle = new CycleId(new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero));
        var forecasts = new[] { "icon_d2", "gfs_seamless" }
            .Select(id => new ModelForecast(
                new WeatherModel(id), "home", cycle, cycle.Timestamp,
                new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3), new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 21.0 })]),
                DailyForecastSeries.Empty))
            .ToList();
        actor.Tell(new PublishTelemetry(forecasts));

        await WaitUntilAsync(
            () => publisher.Published.Count(p => p.Topic.EndsWith("/state")) == 2,
            "two state publishes");
        var state = publisher.Published.Single(p => p.Topic == "njord/home/icon_d2/state");
        Assert.True(state.Retain);
        Assert.Contains("\"h3\"", state.Payload);
    }

    [Fact(Timeout = 5000)]
    public async Task A_disconnect_leads_to_a_reconnect_with_fresh_online_announcement()
    {
        var publisher = new FakePublisher();
        CreateActor(Options("icon_d2"), publisher);
        await WaitUntilAsync(() => publisher.ConnectCount >= 1, "initial connect");

        publisher.SimulateDisconnect();

        await WaitUntilAsync(() => publisher.ConnectCount >= 2, "reconnect after disconnect");
        await WaitUntilAsync(
            () => publisher.Published.Count(p => p is ("njord/status", "online", true)) >= 2,
            "online re-announced");
    }

    [Fact(Timeout = 5000)]
    public async Task Stale_retained_njord_devices_are_tombstoned()
    {
        var publisher = new FakePublisher();
        CreateActor(Options("icon_d2"), publisher);
        await WaitUntilAsync(() => ConfigPublishes(publisher).Count == 1, "discovery done");

        publisher.SimulateInbound("homeassistant/device/njord_home_removed_model/config", """{"dev":{}}""");

        await WaitUntilAsync(
            () => publisher.Published.Any(p =>
                p is ("homeassistant/device/njord_home_removed_model/config", "", true)),
            "tombstone for the removed device");
    }

    [Fact(Timeout = 5000)]
    public async Task Foreign_retained_devices_are_left_alone()
    {
        var publisher = new FakePublisher();
        CreateActor(Options("icon_d2"), publisher);
        await WaitUntilAsync(() => ConfigPublishes(publisher).Count == 1, "discovery done");

        publisher.SimulateInbound("homeassistant/device/zigbee2mqtt_plug/config", """{"dev":{}}""");
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(publisher.Published, p => p.Topic.Contains("zigbee2mqtt"));
    }

    private static List<(string Topic, string Payload, bool Retain)> ConfigPublishes(FakePublisher publisher)
        => [.. publisher.Published.Where(p => p.Topic.EndsWith("/config") && p.Payload.Length > 0)];

    private sealed class FakePublisher : IMqttPublisher
    {
        private Action<string, string>? _onMessage;
        private Action? _onDisconnected;
        private int _connectCount;

        public ConcurrentQueue<(string Topic, string Payload, bool Retain)> PublishLog { get; } = [];
        public ConcurrentQueue<string> SubscriptionLog { get; } = [];

        public IReadOnlyList<(string Topic, string Payload, bool Retain)> Published => [.. PublishLog];
        public IReadOnlyList<string> Subscriptions => [.. SubscriptionLog];
        public int ConnectCount => _connectCount;

        public Task ConnectAsync(Action<string, string> onMessage, Action onDisconnected, CancellationToken cancellationToken)
        {
            _onMessage = onMessage;
            _onDisconnected = onDisconnected;
            Interlocked.Increment(ref _connectCount);
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            PublishLog.Enqueue((topic, payload, retain));
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
        {
            SubscriptionLog.Enqueue(topicFilter);
            return Task.CompletedTask;
        }

        public void SimulateInbound(string topic, string payload) => _onMessage?.Invoke(topic, payload);

        public void SimulateDisconnect() => _onDisconnected?.Invoke();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
