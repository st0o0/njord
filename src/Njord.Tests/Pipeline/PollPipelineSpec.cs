using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;
using Njord.Ingest;
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Tests.Pipeline;

public sealed class PollPipelineSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("poll-pipeline-spec");
    private readonly IMaterializer _materializer;

    public PollPipelineSpec() => _materializer = _system.Materializer();

    public void Dispose() => _system.Dispose();

    private static NjordOptions Options(int locations = 2, params string[] models) => new()
    {
        PollInterval = TimeSpan.FromMilliseconds(200),
        Locations = [.. Enumerable.Range(1, locations).Select(i => new LocationOptions
        {
            Name = $"loc-{i}",
            Latitude = 47.0 + i,
            Longitude = 8.0 + i,
        })],
        Models = [.. models],
    };

    [Fact(Timeout = 5000)]
    public async Task Targets_produce_MqttMessages_through_fetch_and_publish_stages()
    {
        var client = new FakeOpenMeteoClient();
        var options = Options(1, "A", "B");
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var cycle = new CycleId(DateTimeOffset.UtcNow);

        var locA = options.Locations[0];
        var targets = new[]
        {
            new WeightedTarget(locA, new WeatherModel("A"), 1, cycle),
            new WeightedTarget(locA, new WeatherModel("B"), 1, cycle),
        };

        var results = await Source.From(targets)
            .SelectAsyncUnordered(8, async target =>
                await client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None))
            .Collect(outcome => outcome is FetchOutcome.Success s ? s : null!)
            .Where(s => s is not null)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var perHorizon = StatePayloadBuilder.BuildPerHorizon(forecast, parameters, [.. options.Horizons], options.ForecastDays, cycle.Timestamp);
                return perHorizon.Select(kvp =>
                    new MqttMessage(TopicScheme.HorizonTopic(options.Mqtt.BaseTopic, forecast.Location, forecast.Model, kvp.Key), kvp.Value, true));
            })
            .RunWith(Sink.Seq<MqttMessage>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(2 * (options.Horizons.Count + options.ForecastDays), results.Count);
        Assert.All(results, msg => Assert.True(msg.Retain));
        Assert.All(results, msg => Assert.Matches(@"/(h\d+|d\d+)$", msg.Topic));
    }

    [Fact(Timeout = 5000)]
    public async Task Failed_fetches_are_filtered_out()
    {
        var client = new FakeOpenMeteoClient { FailingModels = { "BROKEN" } };
        var options = Options(1, "A", "BROKEN");
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var cycle = new CycleId(DateTimeOffset.UtcNow);

        var loc = options.Locations[0];
        var targets = new[]
        {
            new WeightedTarget(loc, new WeatherModel("A"), 1, cycle),
            new WeightedTarget(loc, new WeatherModel("BROKEN"), 1, cycle),
        };

        var results = await Source.From(targets)
            .SelectAsyncUnordered(8, async target =>
                await client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None))
            .Collect(outcome => outcome is FetchOutcome.Success s ? s : null!)
            .Where(s => s is not null)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var perHorizon = StatePayloadBuilder.BuildPerHorizon(forecast, parameters, [.. options.Horizons], options.ForecastDays, cycle.Timestamp);
                return perHorizon.Select(kvp =>
                    new MqttMessage(TopicScheme.HorizonTopic(options.Mqtt.BaseTopic, forecast.Location, forecast.Model, kvp.Key), kvp.Value, true));
            })
            .RunWith(Sink.Seq<MqttMessage>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(options.Horizons.Count + options.ForecastDays, results.Count);
    }

}
