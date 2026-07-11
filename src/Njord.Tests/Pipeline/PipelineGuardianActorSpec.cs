using Njord.Domain;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class PipelineGuardianActorSpec
{
    [Fact(Timeout = 5000)]
    public void The_cycle_summary_names_every_target_outcome()
    {
        var cycle = new CycleId(new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero));
        var result = new CycleResult(
            cycle,
            Received:
            [
                new ModelForecast(new WeatherModel("ICON-D2"), "home", cycle, cycle.Timestamp,
                    new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3), Temperature: 20.0)])),
            ],
            Failed:
            [
                new FetchOutcome.Failure(cycle, "home", new WeatherModel("ECMWF"), FetchFailureReason.RateLimited, "HTTP 429"),
            ],
            Unanswered: [new FetchTarget("home", new WeatherModel("SWISS1X1"))]);

        var summary = PipelineGuardianActor.FormatSummary(result);

        Assert.Contains("home/ICON-D2 ok", summary);
        Assert.Contains("home/ECMWF failed (RateLimited)", summary);
        Assert.Contains("home/SWISS1X1 unanswered", summary);
    }
}
