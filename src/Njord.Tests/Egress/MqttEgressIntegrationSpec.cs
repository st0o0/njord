using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using Akka.Actor;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;

namespace Njord.Tests.Egress;

/// <summary>
/// Real-broker round trip via Testcontainers/Mosquitto. Gated behind
/// <c>NJORD_DOCKER_TESTS=1</c> because it needs a Docker daemon.
/// </summary>
public sealed class MqttEgressIntegrationSpec
{
    private const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

    // Container pull + broker round trips — far above the unit-test budget.
    [Fact(Timeout = 120000)]
    public async Task The_full_egress_round_trip_works_against_a_real_broker()
    {
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("NJORD_DOCKER_TESTS") != "1",
            "NJORD_DOCKER_TESTS not set to 1 — docker integration test skipped.");
        var ct = TestContext.Current.CancellationToken;

        await using var container = new ContainerBuilder()
            .WithImage("eclipse-mosquitto:2")
            .WithResourceMapping(Encoding.UTF8.GetBytes(MosquittoConf), "/mosquitto/config/mosquitto.conf")
            .WithPortBinding(1883, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version .+ running"))
            .Build();
        await container.StartAsync(ct);
        var mqttOptions = new MqttOptions { Host = "localhost", Port = container.GetMappedPublicPort(1883) };

        // A stale device config from an earlier configuration, retained on the broker.
        var staleTopic = "homeassistant/device/njord_home_stale_model/config";
        await PublishRetainedAsync(mqttOptions, staleTopic, """{"dev":{}}""", ct);

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2", "gfs_seamless"],
            Mqtt = mqttOptions,
        };
        using var system = ActorSystem.Create("egress-integration");
        await using var publisher = new MqttNetPublisher(mqttOptions, NullLogger<MqttNetPublisher>.Instance);
        var actor = system.ActorOf(Props.Create(() => new MqttConnectionActor(
            Microsoft.Extensions.Options.Options.Create(options),
            publisher,
            NullLogger<MqttConnectionActor>.Instance,
            MqttEgressTuning.Default)));

        var tick = new DateTimeOffset(2026, 7, 12, 12, 30, 0, TimeSpan.Zero);
        var series = new ForecastSeries(Enumerable.Range(0, 90)
            .Select(i => new ForecastPoint(
                new DateTimeOffset(2026, 7, 12, 13, 0, 0, TimeSpan.Zero).AddHours(i), Temperature: 20.0 + i)));
        actor.Tell(new PublishTelemetry(
            [new ModelForecast(new WeatherModel("icon_d2"), "home", new CycleId(tick), tick, series)]));

        // Discovery: retained device configs with the full 54-component grid.
        var retained = await CollectRetainedAsync(mqttOptions, ["homeassistant/device/+/config", "njord/#"], ct);
        var config = JsonNode.Parse(retained["homeassistant/device/njord_home_icon_d2/config"])!;
        Assert.Equal(54, config["cmps"]!.AsObject().Count);
        Assert.True(retained.ContainsKey("homeassistant/device/njord_home_gfs_seamless/config"));

        // Availability + telemetry: retained online and a retained state satisfying the templates.
        Assert.Equal("online", retained["njord/status"]);
        var state = JsonNode.Parse(retained["njord/home/icon_d2/state"])!;
        Assert.Equal(23.0, (double?)state["h3"]!["temperature"]);

        // Tombstone in flight: at most the live empty delete-publish is visible.
        if (retained.TryGetValue(staleTopic, out var stalePayload))
        {
            Assert.Equal(string.Empty, stalePayload);
        }

        // Graceful shutdown announces offline (the Last Will covers the ungraceful path).
        await actor.GracefulStop(TimeSpan.FromSeconds(5));
        var afterStop = await CollectRetainedAsync(mqttOptions, ["njord/status", "homeassistant/device/+/config"], ct);
        Assert.Equal("offline", afterStop["njord/status"]);
        // Authoritative tombstone check: a fresh subscriber sees no retained stale config.
        Assert.False(afterStop.ContainsKey(staleTopic), "stale retained config survived on the broker");
        Assert.True(afterStop.ContainsKey("homeassistant/device/njord_home_icon_d2/config"));
    }

    private static async Task PublishRetainedAsync(MqttOptions options, string topic, string payload, CancellationToken ct)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(
            new MqttClientOptionsBuilder().WithTcpServer(options.Host, options.Port).Build(), ct);
        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic(topic).WithPayload(payload).WithRetainFlag().Build(),
            ct);
        await client.DisconnectAsync(cancellationToken: ct);
    }

    /// <summary>Fresh subscriber; returns every retained message seen within the settle window.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> CollectRetainedAsync(
        MqttOptions options, string[] filters, CancellationToken ct)
    {
        var seen = new ConcurrentDictionary<string, string>();
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            seen[e.ApplicationMessage.Topic] = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
            return Task.CompletedTask;
        };
        await client.ConnectAsync(
            new MqttClientOptionsBuilder().WithTcpServer(options.Host, options.Port).Build(), ct);
        foreach (var filter in filters)
        {
            await client.SubscribeAsync(filter, cancellationToken: ct);
        }

        // Retained messages arrive immediately after SUBACK; the window also
        // absorbs the actor's own connect/publish latency.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await client.DisconnectAsync(cancellationToken: ct);
        return seen;
    }
}
