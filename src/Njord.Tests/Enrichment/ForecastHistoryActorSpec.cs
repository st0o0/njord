using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Tests.Shared;

namespace Njord.Tests.Enrichment;

public sealed class ForecastHistoryActorSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather"], [], []);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
    }

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
            new ForecastHistoryActor("lucerne", new HistoryOptions(), Parameters, TimeProvider.System)));

        var response = await actor.Ask<HistoryResponse>(new QueryHistory(), TimeSpan.FromSeconds(2));
        Assert.Empty(response.History.Records);
    }

    [Fact(Timeout = 15000)]
    public async Task Record_and_query_returns_persisted_data()
    {
        var actor = Sys.ActorOf(Props.Create(() =>
            new ForecastHistoryActor("lucerne", new HistoryOptions(), Parameters, TimeProvider.System)));

        actor.Tell(new RecordSnapshot(MakeSnapshot()));

        var response = await actor.Ask<HistoryResponse>(new QueryHistory(), TimeSpan.FromSeconds(3));
        Assert.Single(response.History.Records);
        Assert.Equal("lucerne", response.History.Records[0].Location);
    }

    [Fact(Timeout = 15000)]
    public async Task Multiple_records_accumulate()
    {
        var actor = Sys.ActorOf(Props.Create(() =>
            new ForecastHistoryActor("lucerne", new HistoryOptions(), Parameters, TimeProvider.System)));

        for (var i = 0; i < 5; i++)
            actor.Tell(new RecordSnapshot(MakeSnapshot()));

        var response = await actor.Ask<HistoryResponse>(new QueryHistory(), TimeSpan.FromSeconds(3));
        Assert.Equal(5, response.History.Records.Count);
    }
}
