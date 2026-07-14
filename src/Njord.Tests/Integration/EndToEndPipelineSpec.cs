using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Ingest;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using WireMock.Client;
using WireMock.Net.Testcontainers;

namespace Njord.Tests.Integration;

public sealed class EndToEndPipelineSpec : IAsyncLifetime
{
    private const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

    private readonly WireMockContainer _wireMock = new WireMockContainerBuilder()
        .WithImage()
        .Build();

    private readonly DotNet.Testcontainers.Containers.IContainer _mosquitto = new ContainerBuilder("eclipse-mosquitto:2")
        .WithResourceMapping(Encoding.UTF8.GetBytes(MosquittoConf), "/mosquitto/config/mosquitto.conf")
        .WithPortBinding(1883, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version .+ running"))
        .Build();

    private IWireMockAdminApi _admin = null!;
    private HttpClient _http = null!;
    private MqttOptions _mqttOptions = null!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);
    private static readonly IReadOnlyList<int> Horizons = [3, 6, 12, 24, 48, 72];
    private const int ForecastDays = 4;

    private static string Fixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ingest", "Fixtures", name));

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_wireMock.StartAsync(), _mosquitto.StartAsync());
        _admin = _wireMock.CreateWireMockAdminClient();
        _http = _wireMock.CreateClient();
        _mqttOptions = new MqttOptions
        {
            Host = "localhost",
            Port = _mosquitto.GetMappedPublicPort(1883),
        };

        await ConfigureWireMockFixturesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await Task.WhenAll(_wireMock.DisposeAsync().AsTask(), _mosquitto.DisposeAsync().AsTask());
    }

    private async Task ConfigureWireMockFixturesAsync()
    {
        await _admin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
                Params = [new() { Name = "models", Matchers = [new() { Name = "ExactMatcher", Pattern = "icon_eu" }] }],
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Body = Fixture("openmeteo-icon_eu-96h.json"),
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });
        await _admin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
                Params = [new() { Name = "models", Matchers = [new() { Name = "ExactMatcher", Pattern = "icon_d2" }] }],
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Body = Fixture("openmeteo-icon_d2-96h.json"),
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });
    }

    [Fact(Timeout = 120000)]
    public async Task Full_pipeline_from_api_fetch_to_mqtt_retained_messages()
    {
        var ct = TestContext.Current.CancellationToken;

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_eu", "icon_d2"],
            Mqtt = _mqttOptions,
        };

        // 1. Connect to MQTT broker and publish availability
        await using var publisher = new MqttNetPublisher(_mqttOptions, NullLogger<MqttNetPublisher>.Instance);
        await publisher.ConnectAsync((_, _) => { }, () => { }, ct);
        await publisher.SendAsync(TopicScheme.AvailabilityTopic(options.Mqtt.BaseTopic), "online", retain: true, ct);

        // 2. Fetch forecasts from WireMock
        var client = new OpenMeteoClient(_http, Parameters);
        var cycle = new CycleId(DateTimeOffset.UtcNow);

        var forecasts = new List<ModelForecast>();
        foreach (var modelId in options.Models)
        {
            var outcome = await client.FetchAsync(options.Locations[0], new WeatherModel(modelId), cycle, ct);
            var success = Assert.IsType<FetchOutcome.Success>(outcome);
            forecasts.Add(success.Forecast);
        }

        // 3. Project horizons and publish state payloads
        var anchorTime = DateTimeOffset.UtcNow;
        foreach (var forecast in forecasts)
        {
            var perHorizon = HorizonProjection.BuildPerHorizon(
                forecast, Parameters, Horizons.ToList(), ForecastDays, anchorTime);
            foreach (var (horizon, json) in perHorizon)
            {
                var topic = TopicScheme.HorizonTopic(options.Mqtt.BaseTopic, forecast.Location, forecast.Model, horizon);
                await publisher.SendAsync(topic, json, retain: true, ct);
            }
        }

        // 4. Publish discovery device configs
        foreach (var modelId in options.Models)
        {
            var model = new WeatherModel(modelId);
            var deviceId = TopicScheme.DeviceId("home", model);
            var configTopic = TopicScheme.ConfigTopic(options.Mqtt.DiscoveryPrefix, deviceId);
            var configPayload = DiscoveryPayloadBuilder.Build(
                "home", model, Parameters, Horizons.ToList(), ForecastDays,
                options.Mqtt, options.PollInterval, "test");
            await publisher.SendAsync(configTopic, configPayload, retain: true, ct);
        }

        // 5. Verify retained messages on Mosquitto
        var retained = await CollectRetainedAsync(
            _mqttOptions,
            ["njord/#", "homeassistant/device/+/config"],
            ct);

        // 5.4: Correct number of horizon topics per model
        var expectedPerModel = Horizons.Count + ForecastDays;
        var iconEuHorizonTopics = retained.Keys.Where(t => t.StartsWith("njord/home/icon_eu/")).ToList();
        var iconD2HorizonTopics = retained.Keys.Where(t => t.StartsWith("njord/home/icon_d2/")).ToList();
        Assert.Equal(expectedPerModel, iconEuHorizonTopics.Count);
        Assert.Equal(expectedPerModel, iconD2HorizonTopics.Count);

        // 5.5: Horizon payloads contain expected parameter keys
        var h3Payload = JsonNode.Parse(retained["njord/home/icon_eu/h3"])!;
        Assert.NotNull(h3Payload["temperature"]);

        // 5.6: Discovery device configs present with correct component count
        var expectedHourlyComponents = Parameters.Hourly.Count * Horizons.Count;
        var expectedDailyComponents = Parameters.Daily.Count * ForecastDays;
        var expectedTotal = expectedHourlyComponents + expectedDailyComponents;

        var iconEuConfig = JsonNode.Parse(retained["homeassistant/device/njord_home_icon_eu/config"])!;
        Assert.Equal(expectedTotal, iconEuConfig["cmps"]!.AsObject().Count);
        Assert.True(retained.ContainsKey("homeassistant/device/njord_home_icon_d2/config"));

        // 5.7: Availability topic
        Assert.Equal("online", retained["njord/status"]);
    }

    private static async Task<IReadOnlyDictionary<string, string>> CollectRetainedAsync(
        MqttOptions options, string[] filters, CancellationToken ct)
    {
        var seen = new ConcurrentDictionary<string, string>();
        using var mqttClient = new MqttClientFactory().CreateMqttClient();
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            seen[e.ApplicationMessage.Topic] = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
            return Task.CompletedTask;
        };
        await mqttClient.ConnectAsync(
            new MqttClientOptionsBuilder().WithTcpServer(options.Host, options.Port).Build(), ct);
        foreach (var filter in filters)
        {
            await mqttClient.SubscribeAsync(filter, cancellationToken: ct);
        }

        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await mqttClient.DisconnectAsync(cancellationToken: ct);
        return seen;
    }
}
