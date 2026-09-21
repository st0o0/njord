using Njord.Domain.Weather;
using Njord.Grpc;

namespace Njord.Tests.Grpc;

public sealed class ForecastSnapshotStateSpec
{
    private static readonly DateTimeOffset Anchor = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static ModelForecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        return new ModelForecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries([new ForecastPoint(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 })]),
            DailyForecastSeries.Empty);
    }

    [Fact(Timeout = 5000)]
    public void Apply_adds_forecast_to_state()
    {
        var state = ForecastSnapshotState.Empty;
        var forecast = CreateForecast();
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");

        var next = state.Apply(key, forecast);

        Assert.Single(next.Forecasts);
        Assert.Equal("icon_d2", next.Forecasts[key].Model.Id);
    }

    [Fact(Timeout = 5000)]
    public void Apply_increments_update_counter()
    {
        var state = ForecastSnapshotState.Empty;
        var forecast = CreateForecast();
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");

        var next = state.Apply(key, forecast);

        Assert.Equal(1, next.UpdatesSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void Apply_overwrites_existing_forecast()
    {
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");
        var state = ForecastSnapshotState.Empty
            .Apply(key, CreateForecast())
            .Apply(key, CreateForecast());

        Assert.Single(state.Forecasts);
        Assert.Equal(2, state.UpdatesSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void GetForecast_returns_found_when_exists()
    {
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");
        var state = ForecastSnapshotState.Empty.Apply(key, CreateForecast());

        var response = state.GetForecast(key);

        var found = Assert.IsType<ForecastFound>(response);
        Assert.Equal("icon_d2", found.Forecast.Model.Id);
    }

    [Fact(Timeout = 5000)]
    public void GetForecast_returns_not_found_when_missing()
    {
        var response = ForecastSnapshotState.Empty.GetForecast("nonexistent|key");

        Assert.IsType<ForecastNotFound>(response);
    }

    [Fact(Timeout = 5000)]
    public void GetAllForecasts_returns_all_entries()
    {
        var state = ForecastSnapshotState.Empty
            .Apply(ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2"), CreateForecast("icon_d2"))
            .Apply(ForecastSnapshotStateExtensions.MakeKey("lucerne", "ecmwf"), CreateForecast("ecmwf"));

        var result = state.GetAllForecasts();

        Assert.Equal(2, result.Forecasts.Count);
    }

    [Fact(Timeout = 5000)]
    public void GetPersistenceState_returns_valid_dto()
    {
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");
        var state = ForecastSnapshotState.Empty.Apply(key, CreateForecast());

        var dto = state.GetPersistenceState();

        Assert.Single(dto.Forecasts);
        Assert.True(dto.Forecasts.ContainsKey(key));
    }

    [Fact(Timeout = 5000)]
    public void FromPersistence_roundtrip_preserves_state()
    {
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");
        var original = ForecastSnapshotState.Empty.Apply(key, CreateForecast());

        var restored = ForecastSnapshotStateExtensions.FromPersistence(original.GetPersistenceState());

        Assert.Single(restored.Forecasts);
        Assert.Equal("icon_d2", restored.Forecasts[key].Model.Id);
        Assert.Equal(0, restored.UpdatesSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void ResetSnapshotCounter_zeroes_counter()
    {
        var key = ForecastSnapshotStateExtensions.MakeKey("lucerne", "icon_d2");
        var state = ForecastSnapshotState.Empty.Apply(key, CreateForecast());
        Assert.Equal(1, state.UpdatesSinceSnapshot);

        var reset = state.ResetSnapshotCounter();

        Assert.Equal(0, reset.UpdatesSinceSnapshot);
        Assert.Single(reset.Forecasts);
    }
}
