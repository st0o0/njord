using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class FetchStageSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("fetch-stage-spec");
    private readonly IMaterializer _materializer;

    public FetchStageSpec() => _materializer = _system.Materializer();

    public void Dispose() => _system.Dispose();

    private static WeightedTarget Target(string location = "lucerne", string model = "icon_d2") =>
        new(new LocationOptions { Name = location, Latitude = 47.0, Longitude = 8.3 },
            new WeatherModel(model), 1);

    [Fact(Timeout = 5000)]
    public async Task Successful_fetch_emits_success_outcome()
    {
        var client = new FakeClient();
        var flow = FetchStage.Create(client, TimeProvider.System);

        var results = await Source.Single(Target())
            .Via(flow)
            .RunWith(Sink.Seq<FetchOutcome>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        var outcome = Assert.Single(results);
        Assert.IsType<FetchOutcome.Success>(outcome);
    }

    [Fact(Timeout = 5000)]
    public async Task Transient_failure_emits_failure_outcome_without_killing_stream()
    {
        var client = new FakeClient { FailingModels = { "broken" } };
        var flow = FetchStage.Create(client, TimeProvider.System);

        var targets = new[] { Target(model: "broken"), Target(model: "icon_d2") };
        var results = await Source.From(targets)
            .Via(flow)
            .RunWith(Sink.Seq<FetchOutcome>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r is FetchOutcome.Success);
        Assert.Contains(results, r => r is FetchOutcome.Failure);
    }

    [Fact(Timeout = 5000)]
    public async Task Exception_in_fetch_does_not_kill_stream()
    {
        var client = new FakeClient { ThrowingModels = { "explode" } };
        var flow = FetchStage.Create(client, TimeProvider.System);

        var targets = new[] { Target(model: "explode"), Target(model: "icon_d2") };
        var results = await Source.From(targets)
            .Via(flow)
            .RunWith(Sink.Seq<FetchOutcome>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Contains(results, r => r is FetchOutcome.Success);
    }

    [Fact(Timeout = 5000)]
    public async Task Respects_parallelism_cap()
    {
        var client = new FakeClient { FetchDelay = TimeSpan.FromMilliseconds(100) };
        var flow = FetchStage.Create(client, TimeProvider.System, parallelism: 2);

        var targets = Enumerable.Range(0, 4).Select(i => Target(model: $"m{i}")).ToArray();
        var results = await Source.From(targets)
            .Via(flow)
            .RunWith(Sink.Seq<FetchOutcome>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(4, results.Count);
        Assert.True(client.MaxConcurrent <= 2, $"Expected max 2 concurrent, got {client.MaxConcurrent}");
    }

    private sealed class FakeClient : IOpenMeteoClient
    {
        public HashSet<string> FailingModels { get; } = [];
        public HashSet<string> ThrowingModels { get; } = [];
        public TimeSpan FetchDelay { get; set; } = TimeSpan.Zero;

        private int _concurrent;
        public int MaxConcurrent { get; private set; }

        public async Task<FetchOutcome> FetchAsync(
            LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _concurrent);
            lock (this) { if (current > MaxConcurrent) MaxConcurrent = current; }

            try
            {
                if (ThrowingModels.Contains(model.Id))
                    throw new InvalidOperationException("boom");

                if (FetchDelay > TimeSpan.Zero)
                    await Task.Delay(FetchDelay, cancellationToken);

                if (FailingModels.Contains(model.Id))
                    return new FetchOutcome.Failure(cycle, location.Name, model, FetchFailureReason.Transport, "simulated");

                return new FetchOutcome.Success(new ModelForecast(
                    model, location.Name, cycle, cycle.Timestamp,
                    new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3),
                        new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
                    DailyForecastSeries.Empty));
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }
    }
}
