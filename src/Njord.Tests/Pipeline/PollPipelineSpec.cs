using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class PollPipelineSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("poll-pipeline-spec");
    private readonly IMaterializer _materializer;

    public PollPipelineSpec() => _materializer = _system.Materializer();

    public void Dispose() => _system.Dispose();

    private static NjordOptions Options(int locations, params string[] models) => new()
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

    private async Task<IReadOnlyList<CycleResult>> CollectAsync(
        NjordOptions options,
        FakeOpenMeteoClient client,
        int count,
        TimeSpan? window = null,
        RestartSettings? restartSettings = null)
    {
        var results = await PollPipeline
            .Create(options, client, TimeProvider.System, _materializer, window ?? TimeSpan.FromSeconds(1), restartSettings)
            .Take(count)
            .RunWith(Sink.Seq<CycleResult>(), _materializer)
            .WaitAsync(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        return [.. results];
    }

    [Fact(Timeout = 5000)]
    public async Task Each_cycle_fans_out_over_every_location_model_pair()
    {
        var client = new FakeOpenMeteoClient();
        var result = (await CollectAsync(Options(2, "A", "B", "C", "D"), client, count: 1)).Single();

        Assert.Equal(8, result.Received.Count);
        Assert.Empty(result.Failed);
        Assert.Empty(result.Unanswered);
        Assert.Equal(8, client.Requests.Count(r => r.Cycle == result.Cycle));
        Assert.Equal(8, client.Requests.Where(r => r.Cycle == result.Cycle).Distinct().Count());
    }

    [Fact(Timeout = 5000)]
    public async Task A_hanging_model_does_not_block_the_cycle_result()
    {
        var client = new FakeOpenMeteoClient { HangingModels = { "SLOW" } };

        var result = (await CollectAsync(Options(1, "A", "B", "SLOW"), client, count: 1, window: TimeSpan.FromMilliseconds(500))).Single();

        Assert.Equal(2, result.Received.Count);
        var unanswered = Assert.Single(result.Unanswered);
        Assert.Equal(new WeatherModel("SLOW"), unanswered.Model);
    }

    [Fact(Timeout = 5000)]
    public async Task Failed_fetch_outcomes_are_reported_not_fatal()
    {
        var client = new FakeOpenMeteoClient { FailingModels = { "BROKEN" } };

        var result = (await CollectAsync(Options(1, "A", "BROKEN"), client, count: 1)).Single();

        Assert.Single(result.Received);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(new WeatherModel("BROKEN"), failure.Model);
        Assert.Empty(result.Unanswered);
    }

    [Fact(Timeout = 5000)]
    public async Task A_thrown_exception_restarts_the_pipeline_instead_of_killing_it()
    {
        var client = new FakeOpenMeteoClient { ThrowOnFirstCall = true };
        var restart = RestartSettings.Create(
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(400), 0.2);

        var result = (await CollectAsync(Options(1, "A"), client, count: 1, restartSettings: restart)).Single();

        Assert.Single(result.Received);
        Assert.True(client.Requests.Count >= 2, "expected a second attempt after the restart");
    }

    [Fact(Timeout = 5000)]
    public async Task Consecutive_cycles_produce_one_result_each()
    {
        var client = new FakeOpenMeteoClient();

        var results = await CollectAsync(Options(1, "A"), client, count: 2);

        Assert.Equal(2, results.Count);
        Assert.NotEqual(results[0].Cycle, results[1].Cycle);
    }

    private sealed class FakeOpenMeteoClient : IOpenMeteoClient
    {
        public ConcurrentQueue<(CycleId Cycle, string Location, string Model)> RequestLog { get; } = [];
        public HashSet<string> HangingModels { get; } = [];
        public HashSet<string> FailingModels { get; } = [];
        public bool ThrowOnFirstCall { get; set; }

        public IReadOnlyList<(CycleId Cycle, string Location, string Model)> Requests => [.. RequestLog];

        public async Task<FetchOutcome> FetchAsync(
            LocationOptions location, WeatherModel model, CycleId cycle, CancellationToken cancellationToken)
        {
            RequestLog.Enqueue((cycle, location.Name, model.Id));

            if (ThrowOnFirstCall)
            {
                ThrowOnFirstCall = false;
                throw new InvalidOperationException("boom");
            }

            if (HangingModels.Contains(model.Id))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (FailingModels.Contains(model.Id))
            {
                return new FetchOutcome.Failure(cycle, location.Name, model, FetchFailureReason.RateLimited, "HTTP 429");
            }

            var validAt = cycle.Timestamp.AddHours(3);
            return new FetchOutcome.Success(new ModelForecast(
                model,
                location.Name,
                cycle,
                cycle.Timestamp,
                new ForecastSeries([new ForecastPoint(validAt, new Dictionary<ParameterDef, double?> { [ParameterRegistry.GetByApiName("temperature_2m")!] = 20.0 })]),
                DailyForecastSeries.Empty));
        }
    }
}
