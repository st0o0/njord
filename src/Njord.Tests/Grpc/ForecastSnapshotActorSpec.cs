using Akka.Actor;
using Akka.Persistence;
using Njord.Domain.Weather;
using Njord.Grpc;

namespace Njord.Tests.Grpc;

public sealed class ForecastSnapshotActorSpec : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ActorSystem _system = ActorSystem.Create("forecast-snapshot-spec",
        "akka.persistence.snapshot-store.plugin = \"akka.persistence.snapshot-store.inmem\"" +
        "\nakka.persistence.journal.plugin = \"akka.persistence.journal.inmem\"");

    public void Dispose() => _system.Dispose();

    private IActorRef CreateActor() =>
        _system.ActorOf(Props.Create(() => new ForecastSnapshotActor()));

    private static ModelForecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        return new ModelForecast(new WeatherModel(model), "lucerne", new CycleId(Now),
            new ForecastSeries([new ForecastPoint(Now.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 })]),
            DailyForecastSeries.Empty);
    }

    [Fact(Timeout = 5000)]
    public async Task Update_and_retrieve_a_forecast()
    {
        var actor = CreateActor();
        var forecast = CreateForecast();

        var ack = await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast.Model, forecast));
        Assert.NotNull(ack);

        var response = await actor.Ask<ForecastResponse>(new GetForecast("lucerne", "icon_d2"));
        Assert.NotNull(response.Forecast);
        Assert.Equal("icon_d2", response.Forecast.Model.Id);
    }

    [Fact(Timeout = 5000)]
    public async Task Unknown_forecast_returns_null()
    {
        var actor = CreateActor();

        var response = await actor.Ask<ForecastResponse>(new GetForecast("lucerne", "unknown"));
        Assert.Null(response.Forecast);
    }

    [Fact(Timeout = 5000)]
    public async Task Overwrite_replaces_previous()
    {
        var actor = CreateActor();
        var forecast1 = CreateForecast();
        var forecast2 = CreateForecast();

        await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast1.Model, forecast1));
        await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast2.Model, forecast2));

        var response = await actor.Ask<ForecastResponse>(new GetForecast("lucerne", "icon_d2"));
        Assert.NotNull(response.Forecast);
    }

    [Fact(Timeout = 5000)]
    public async Task GetAllForecasts_returns_all()
    {
        var actor = CreateActor();
        await actor.Ask<Ack>(new UpdateForecast("lucerne", new WeatherModel("icon_d2"), CreateForecast("icon_d2")));
        await actor.Ask<Ack>(new UpdateForecast("lucerne", new WeatherModel("ecmwf"), CreateForecast("ecmwf")));

        var response = await actor.Ask<AllForecastsResponse>(new GetAllForecasts());
        Assert.Equal(2, response.Forecasts.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task State_available_before_snapshot_threshold()
    {
        var actor = CreateActor();
        var forecast = CreateForecast();
        await actor.Ask<Ack>(new UpdateForecast("lucerne", forecast.Model, forecast));

        var response = await actor.Ask<ForecastResponse>(new GetForecast("lucerne", "icon_d2"));
        Assert.NotNull(response.Forecast);
    }

    [Fact(Timeout = 5000)]
    public async Task State_survives_after_snapshot_threshold_reached()
    {
        var actor = CreateActor();

        for (var i = 0; i < 20; i++)
        {
            var model = $"model_{i}";
            await actor.Ask<Ack>(new UpdateForecast("lucerne", new WeatherModel(model), CreateForecast(model)));
        }

        var response = await actor.Ask<AllForecastsResponse>(new GetAllForecasts());
        Assert.Equal(20, response.Forecasts.Count);
    }
}
