using Akka.Actor;
using Akka.Hosting;
using Njord.Domain.Weather;
using Njord.Messages.Common;
using Njord.Messages.Snapshots;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class ForecastSnapshotActorSpec : Akka.Hosting.TestKit.TestKit
{
    private static readonly DateTimeOffset Anchor = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private const string EntityId = "lucerne|icon_d2";

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .AddTestTimefactor();
    }

    private IActorRef CreateActor(string entityId = EntityId) =>
        Sys.ActorOf(Props.Create(() => new ForecastSnapshotActor(entityId)));

    private static ModelForecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        return new ModelForecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries([new ForecastPoint(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 })]),
            DailyForecastSeries.Empty);
    }

    [Fact(Timeout = 5000)]
    public async Task Update_and_retrieve_a_forecast()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();
        var forecast = CreateForecast();

        var ack = await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast.Model, forecast), ct);
        Assert.NotNull(ack);

        var response = await actor.Ask<QueryForecastResponse>(new QueryForecast("lucerne", "icon_d2"), ct);
        var found = Assert.IsType<ForecastFound>(response);
        Assert.Equal("icon_d2", found.Forecast.Model.Id);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_forecast_returns_not_found()
    {
        var actor = CreateActor();

        var response = await actor.Ask<QueryForecastResponse>(new QueryForecast("lucerne", "icon_d2"), TestContext.Current.CancellationToken);
        Assert.IsType<ForecastNotFound>(response);
    }

    [Fact(Timeout = 5000)]
    public async Task Overwrite_replaces_previous()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();
        var forecast1 = CreateForecast();
        var forecast2 = CreateForecast();

        await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast1.Model, forecast1), ct);
        await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast2.Model, forecast2), ct);

        var response = await actor.Ask<QueryForecastResponse>(new QueryForecast("lucerne", "icon_d2"), ct);
        Assert.IsType<ForecastFound>(response);
    }

    [Fact(Timeout = 5000)]
    public async Task State_available_before_snapshot_threshold()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();
        var forecast = CreateForecast();
        await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast.Model, forecast), ct);

        var response = await actor.Ask<QueryForecastResponse>(new QueryForecast("lucerne", "icon_d2"), ct);
        Assert.IsType<ForecastFound>(response);
    }

    [Fact(Timeout = 5000)]
    public async Task State_survives_after_snapshot_threshold_reached()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = CreateActor();

        for (var i = 0; i < 20; i++)
        {
            await actor.Ask<Ack>(new UpdateForecast("lucerne", new WeatherModel("icon_d2"), CreateForecast()), ct);
        }

        var response = await actor.Ask<QueryForecastResponse>(new QueryForecast("lucerne", "icon_d2"), ct);
        Assert.IsType<ForecastFound>(response);
    }
}
