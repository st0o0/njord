using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class PipelineGuardianActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("guardian-spec");

    public void Dispose() => _system.Dispose();

    [Fact(Timeout = 5000)]
    public void The_cycle_summary_names_every_target_outcome()
    {
        var cycle = new CycleId(new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero));
        var result = new CycleResult(
            cycle,
            Received:
            [
                new ModelForecast(new WeatherModel("icon_d2"), "home", cycle, cycle.Timestamp,
                    new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3), new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
                    DailyForecastSeries.Empty),
            ],
            Failed:
            [
                new FetchOutcome.Failure(cycle, "home", new WeatherModel("ecmwf_ifs025"), FetchFailureReason.RateLimited, "HTTP 429"),
            ],
            Unanswered: [new FetchTarget("home", new WeatherModel("meteoswiss_icon_ch1"))]);

        var summary = PipelineGuardianActor.FormatSummary(result);

        Assert.Contains("home/icon_d2 ok", summary);
        Assert.Contains("home/ecmwf_ifs025 failed (RateLimited)", summary);
        Assert.Contains("home/meteoswiss_icon_ch1 unanswered", summary);
    }

    [Fact(Timeout = 5000)]
    public async Task Cycle_results_are_forwarded_to_the_egress_as_domain_telemetry()
    {
        var received = new ConcurrentQueue<object>();
        var recorder = _system.ActorOf(Props.Create(() => new RecordingActor(received)));
        var registry = new ActorRegistry();
        registry.Register<MqttConnectionActor>(recorder);
        var options = new NjordOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(200),
            Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Mqtt = new MqttOptions { Host = "broker.local" },
        };

        _system.ActorOf(Props.Create(() => new PipelineGuardianActor(
            Microsoft.Extensions.Options.Options.Create(options),
            new StubClient(),
            TimeProvider.System,
            NullLogger<PipelineGuardianActor>.Instance,
            registry)));

        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (!received.OfType<PublishTelemetry>().Any())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out waiting for telemetry hand-off");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        var telemetry = received.OfType<PublishTelemetry>().First();
        var forecast = Assert.Single(telemetry.Forecasts);
        Assert.Equal("home", forecast.Location);
        Assert.Equal(new WeatherModel("icon_d2"), forecast.Model);
    }

    private sealed class RecordingActor : ReceiveActor
    {
        public RecordingActor(ConcurrentQueue<object> sink) => ReceiveAny(sink.Enqueue);
    }

    private sealed class StubClient : IOpenMeteoClient
    {
        public Task<FetchOutcome> FetchAsync(
            LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
            => Task.FromResult<FetchOutcome>(new FetchOutcome.Success(new ModelForecast(
                model,
                location.Name,
                cycle,
                cycle.Timestamp,
                new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3), new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
                DailyForecastSeries.Empty)));
    }
}
