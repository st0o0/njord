using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Egress;

/// <summary>
/// Real-broker round trip via Testcontainers/Mosquitto. Gated behind
/// <c>NJORD_DOCKER_TESTS=1</c> because it needs a Docker daemon.
/// </summary>
public sealed class MqttEgressIntegrationSpec
{
    private const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

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

        var staleTopic = "homeassistant/device/njord_home_stale_model/config";
        await PublishRetainedAsync(mqttOptions, staleTopic, """{"dev":{}}""", ct);

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2", "gfs_seamless"],
            Mqtt = mqttOptions,
        };
        using var system = ActorSystem.Create("egress-integration");
        await using var mqttClient = new MqttNetPublisher(mqttOptions, NullLogger<MqttNetPublisher>.Instance);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var registry = ActorRegistry.For(system);
        var fakePipeline = system.ActorOf(Props.Create(() => new FakePipelineSource(system.Materializer())));
        registry.Register<PipelineActor>(fakePipeline);
        var actor = system.ActorOf(Props.Create(() => new MqttEgressActor(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new Njord.Configuration.EnrichmentOptions()),
            mqttClient,
            mqttClient,
            NullLogger<MqttEgressActor>.Instance,
            MqttEgressTuning.Default,
            parameters,
            TimeProvider.System)));

        // Simulate a state publish via the transport (as the pipeline would through StreamRef)
        var tick = new DateTimeOffset(2026, 7, 12, 12, 30, 0, TimeSpan.Zero);
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var series = new ForecastSeries(Enumerable.Range(0, 90)
            .Select(i => new ForecastPoint(
                new DateTimeOffset(2026, 7, 12, 13, 0, 0, TimeSpan.Zero).AddHours(i),
                new Dictionary<ParameterDef, double?> { [temp] = 20.0 + i })));
        var forecast = new ModelForecast(new WeatherModel("icon_d2"), "home", new CycleId(tick), series, DailyForecastSeries.Empty);
        var perHorizon = StatePayloadBuilder.BuildPerHorizon(forecast, parameters, options.Horizons.ToList(), options.ForecastDays, tick);
        foreach (var (horizon, json) in perHorizon)
        {
            var horizonTopic = TopicScheme.HorizonTopic(options.Mqtt.BaseTopic, forecast.Location, forecast.Model, horizon);
            await mqttClient.SendAsync(horizonTopic, json, retain: true, ct);
        }

        // Discovery: retained device configs with the component grid.
        var expectedHourlyComponents = parameters.Hourly.Count * options.Horizons.Count;
        var expectedDailyComponents = parameters.Daily.Count * options.ForecastDays;
        var expectedTotal = expectedHourlyComponents + expectedDailyComponents;
        var retained = await CollectRetainedAsync(mqttOptions, ["homeassistant/device/+/config", "njord/#"], ct);
        var config = JsonNode.Parse(retained["homeassistant/device/njord_home_icon_d2/config"])!;
        Assert.Equal(expectedTotal, config["cmps"]!.AsObject().Count);
        Assert.True(retained.ContainsKey("homeassistant/device/njord_home_gfs_seamless/config"));

        Assert.Equal("online", retained["njord/status"]);
        var h3State = JsonNode.Parse(retained["njord/home/icon_d2/h3"])!;
        Assert.Equal(23.0, (double?)h3State["temperature"]);

        if (retained.TryGetValue(staleTopic, out var stalePayload))
        {
            Assert.Equal(string.Empty, stalePayload);
        }

        await actor.GracefulStop(TimeSpan.FromSeconds(5));
        var afterStop = await CollectRetainedAsync(mqttOptions, ["njord/status", "homeassistant/device/+/config"], ct);
        Assert.Equal("offline", afterStop["njord/status"]);
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

        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await client.DisconnectAsync(cancellationToken: ct);
        return seen;
    }

    private sealed class FakePipelineSource : ReceiveActor
    {
        public FakePipelineSource(Akka.Streams.IMaterializer mat)
        {
            Receive<RequestPipelineSource>(_ =>
            {
                var sourceRef = Akka.Streams.Dsl.Source.Empty<FetchOutcome>()
                    .RunWith(Akka.Streams.Dsl.StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;
                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }
    }
}
