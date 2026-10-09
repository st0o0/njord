using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Messages.Enrichment;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Enrichment.Tests;

public sealed class ForecastHistoryActorSpec : NjordActorSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.ForecastEpoch;
    private static readonly FakeTimeProvider Time = TestTimeouts.AtEpoch(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather"], [], []);

    private static ModelSnapshot MakeSnapshot()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?>
            {
                [Temperature] = 20.0 + h * 0.5,
            })).ToList();
        var forecast = new ModelForecast(new("icon_d2"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        return ModelSnapshot.Empty.Update(forecast);
    }

    [Fact(Timeout = 15000)]
    public async Task Query_returns_empty_history_initially()
    {
        var actor = Sys.ActorOf(Props.Create(() =>
            new ForecastHistoryActor("lucerne", Options.Create(new EnrichmentOptions()), Parameters, Time)));

        var response = await actor.Ask<QueryHistoryCompleted>(new QueryHistory("lucerne"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Empty(response.History.Records);
    }

    [Fact(Timeout = 15000)]
    public async Task Record_and_query_returns_persisted_data()
    {
        var actor = Sys.ActorOf(Props.Create(() =>
            new ForecastHistoryActor("lucerne", Options.Create(new EnrichmentOptions()), Parameters, Time)));

        actor.Tell(new RecordSnapshot("lucerne", MakeSnapshot()));

        var response = await actor.Ask<QueryHistoryCompleted>(new QueryHistory("lucerne"), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        var record = Assert.Single(response.History.Records);
        Assert.Equal("lucerne", record.Location);
    }

    [Fact(Timeout = 15000)]
    public async Task Multiple_records_accumulate()
    {
        var actor = Sys.ActorOf(Props.Create(() =>
            new ForecastHistoryActor("lucerne", Options.Create(new EnrichmentOptions()), Parameters, Time)));

        for (var i = 0; i < 5; i++)
        {
            actor.Tell(new RecordSnapshot("lucerne", MakeSnapshot()));
        }

        var response = await actor.Ask<QueryHistoryCompleted>(new QueryHistory("lucerne"), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(5, response.History.Records.Count);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Delete_messages_failure_is_logged_as_warning()
    {
        var warnings = CreateTestProbe();
        Sys.EventStream.Subscribe(warnings, typeof(Warning));
        var actor = CreateHistoryActor();

        actor.Tell(new DeleteMessagesFailure(new InvalidOperationException("boom-messages"), 5));

        var warning = await warnings.FishForMessageAsync<Warning>(
            w => w.Message.ToString()!.Contains("Delete messages failed for forecast-history-lucerne"),
            TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("boom-messages", warning.Cause.Message);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Delete_snapshots_failure_is_logged_as_warning()
    {
        var warnings = CreateTestProbe();
        Sys.EventStream.Subscribe(warnings, typeof(Warning));
        var actor = CreateHistoryActor();

        actor.Tell(new DeleteSnapshotsFailure(new SnapshotSelectionCriteria(4), new InvalidOperationException("boom-snapshots")));

        var warning = await warnings.FishForMessageAsync<Warning>(
            w => w.Message.ToString()!.Contains("Delete snapshots failed for forecast-history-lucerne"),
            TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("boom-snapshots", warning.Cause.Message);
    }
    private IActorRef CreateHistoryActor() =>
        Sys.ActorOf(Props.Create(() =>
            new ForecastHistoryActor("lucerne", Options.Create(new EnrichmentOptions()), Parameters, Time)));
}
