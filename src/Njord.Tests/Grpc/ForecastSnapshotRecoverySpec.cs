using Akka.Actor;
using Akka.Persistence.TestKit;
using Njord.Domain.Weather;
using Njord.Grpc;

namespace Njord.Tests.Grpc;

public sealed class ForecastSnapshotRecoverySpec : PersistenceTestKit
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private IActorRef CreateActor() =>
        Sys.ActorOf(Props.Create(() => new ForecastSnapshotActor()));

    private static ModelForecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        return new ModelForecast(new WeatherModel(model), "lucerne", new CycleId(TestTime),
            new ForecastSeries([new ForecastPoint(TestTime.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 })]),
            DailyForecastSeries.Empty);
    }

    private async Task FillToSnapshotThreshold(IActorRef actor, int count = 20)
    {
        for (var i = 0; i < count; i++)
        {
            var model = $"model_{i}";
            await actor.Ask<Ack>(new UpdateForecast("lucerne", new WeatherModel(model), CreateForecast(model)));
        }
    }

    [Fact(Timeout = 5000)]
    public async Task State_recovers_from_snapshot_after_actor_restart()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var response = await recovered.Ask<AllForecastsResponse>(new GetAllForecasts(), TimeSpan.FromSeconds(3));
        Assert.Equal(20, response.Forecasts.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task Updates_below_snapshot_threshold_are_lost_on_restart()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor, count: 5);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var response = await recovered.Ask<AllForecastsResponse>(new GetAllForecasts(), TimeSpan.FromSeconds(3));
        Assert.Empty(response.Forecasts);
    }

    [Fact(Timeout = 5000)]
    public async Task Actor_accepts_updates_after_recovery()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var ack = await recovered.Ask<Ack>(
            new UpdateForecast("zurich", new WeatherModel("gfs"), CreateForecast("gfs")),
            TimeSpan.FromSeconds(3));
        Assert.NotNull(ack);

        var response = await recovered.Ask<ForecastResponse>(
            new GetForecast("zurich", "gfs"), TimeSpan.FromSeconds(3));
        Assert.NotNull(response.Forecast);
        Assert.Equal("gfs", response.Forecast.Model.Id);
    }

    [Fact(Timeout = 5000)]
    public async Task Snapshot_load_failure_during_recovery_kills_actor()
    {
        var actor = CreateActor();
        await FillToSnapshotThreshold(actor);
        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        await WithSnapshotLoad(load => load.Fail(), async () =>
        {
            var recovered = CreateActor();
            Watch(recovered);
            await ExpectTerminatedAsync(recovered);
        });
    }
}
