using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Tests.Integration.Mqtt;

public sealed class MqttEgressIntegrationSpec
{
    [Fact(Timeout = 120000)]
    public async Task The_full_egress_round_trip_works_against_a_real_broker()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var container = new ContainerBuilder("eclipse-mosquitto:2")
            .WithResourceMapping(Encoding.UTF8.GetBytes(MosquittoHelper.MosquittoConf), "/mosquitto/config/mosquitto.conf")
            .WithPortBinding(1883, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version .+ running"))
            .Build();
        await container.StartAsync(ct);
        var mqttOptions = new MqttOptions { Host = "localhost", Port = container.GetMappedPublicPort(1883) };

        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2", "gfs_seamless"],
            Mqtt = mqttOptions,
        };

        await using var mqttClient = new MqttNetPublisher(mqttOptions, NullLogger<MqttNetPublisher>.Instance);
        await mqttClient.ConnectAsync((_, _) => { }, () => { }, ct);
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        await mqttClient.SendAsync(TopicScheme.AvailabilityTopic(options.Mqtt.BaseTopic), "online", retain: true, ct);

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

        foreach (var modelId in options.Models)
        {
            var model = new WeatherModel(modelId);
            var deviceId = TopicScheme.DeviceId("home", model);
            var configTopic = TopicScheme.ConfigTopic(options.Mqtt.DiscoveryPrefix, deviceId);
            var configPayload = DiscoveryPayloadBuilder.Build(
                "home", model, parameters, options.Horizons.ToList(), options.ForecastDays,
                options.Mqtt, options.PollInterval, "test");
            await mqttClient.SendAsync(configTopic, configPayload, retain: true, ct);
        }

        var expectedHourlyComponents = parameters.Hourly.Count * options.Horizons.Count;
        var expectedDailyComponents = parameters.Daily.Count * options.ForecastDays;
        var expectedTotal = expectedHourlyComponents + expectedDailyComponents;
        var retained = await MosquittoHelper.CollectRetainedAsync(mqttOptions, ["homeassistant/device/+/config", "njord/#"], ct);

        var config = JsonNode.Parse(retained["homeassistant/device/njord_home_icon_d2/config"])!;
        Assert.Equal(expectedTotal, config["cmps"]!.AsObject().Count);
        Assert.True(retained.ContainsKey("homeassistant/device/njord_home_gfs_seamless/config"));

        Assert.Equal("online", retained["njord/status"]);
        var h3State = JsonNode.Parse(retained["njord/home/icon_d2/h3"])!;
        Assert.Equal(23.0, (double?)h3State["temperature"]);
    }
}
