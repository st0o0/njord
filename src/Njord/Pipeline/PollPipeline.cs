using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Pipeline;

public static class PollPipeline
{
    private const int FetchParallelism = 4;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Tick → one cycle at a time (SelectAsync(1)) → inner fan-out stream per
    /// cycle (throttle → fetch → TakeWithin) → exactly one CycleResult per
    /// cycle. Cycles never overlap, so a straggling response can never re-open
    /// a closed cycle; the whole source restarts with backoff on failure.
    /// </summary>
    public static Source<CycleResult, NotUsed> Create(
        NjordOptions options,
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IMaterializer materializer,
        TimeSpan? aggregationWindow = null,
        RestartSettings? restartSettings = null)
    {
        var budget = options.EffectiveBudget;

        var targets = options.Locations
            .SelectMany(location => options.Models.Select(model => (Location: location, Model: new WeatherModel(model))))
            .ToList();

        // The window must cover the throttled fan-out plus one HTTP timeout.
        var window = aggregationWindow
            ?? TimeSpan.FromMinutes((double)targets.Count / budget.RequestsPerMinute)
                + TimeSpan.FromSeconds(60);

        var settings = restartSettings
            ?? RestartSettings.Create(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), 0.2);

        return RestartSource.WithBackoff(
            () => Source.Tick(InitialDelay, options.PollInterval, NotUsed.Instance)
                .Select(_ => CycleId.From(timeProvider))
                .SelectAsync(1, cycle => RunCycleAsync(cycle, targets, budget, client, window, materializer)),
            settings);
    }

    private static async Task<CycleResult> RunCycleAsync(
        CycleId cycle,
        IReadOnlyList<(LocationOptions Location, WeatherModel Model)> targets,
        RequestBudget budget,
        IOpenMeteoClient client,
        TimeSpan window,
        IMaterializer materializer)
    {
        var outcomes = await Source.From(targets)
            .Throttle(budget.RequestsPerMinute, TimeSpan.FromMinutes(1), budget.RequestsPerMinute, ThrottleMode.Shaping)
            .SelectAsyncUnordered(FetchParallelism, t => client.FetchAsync(t.Location, t.Model, cycle, CancellationToken.None))
            .TakeWithin(window)
            .RunWith(Sink.Seq<FetchOutcome>(), materializer);

        var received = outcomes.OfType<FetchOutcome.Success>().Select(s => s.Forecast).ToList();
        var failed = outcomes.OfType<FetchOutcome.Failure>().ToList();

        var answered = received.Select(f => new FetchTarget(f.Location, f.Model))
            .Concat(failed.Select(f => new FetchTarget(f.Location, f.Model)))
            .ToHashSet();
        var unanswered = targets
            .Select(t => new FetchTarget(t.Location.Name, t.Model))
            .Where(t => !answered.Contains(t))
            .ToList();

        return new CycleResult(cycle, received, failed, unanswered);
    }
}
