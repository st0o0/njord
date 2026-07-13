using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class ModelSnapshotSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly WeatherModel Ecmwf = new("ecmwf_ifs025");

    private static ModelForecast Forecast(string location, WeatherModel model, CycleId cycle)
        => new(model, location, cycle, new ForecastSeries([]), DailyForecastSeries.Empty);

    [Fact(Timeout = 5000)]
    public void Empty_snapshot_has_no_entries()
    {
        Assert.Empty(ModelSnapshot.Empty.Entries);
    }

    [Fact(Timeout = 5000)]
    public void Empty_snapshot_has_changed_false()
    {
        Assert.False(ModelSnapshot.Empty.HasChanged);
    }

    [Fact(Timeout = 5000)]
    public void First_update_adds_entry_and_sets_has_changed()
    {
        var cycle = new CycleId(T0);
        var snapshot = ModelSnapshot.Empty.Update(Forecast("lucerne", IconD2, cycle));

        Assert.Single(snapshot.Entries);
        Assert.True(snapshot.HasChanged);
    }

    [Fact(Timeout = 5000)]
    public void Update_replaces_existing_entry_on_new_cycle()
    {
        var cycle1 = new CycleId(T0);
        var cycle2 = new CycleId(T0.AddHours(1));

        var snapshot = ModelSnapshot.Empty
            .Update(Forecast("lucerne", IconD2, cycle1))
            .Update(Forecast("lucerne", IconD2, cycle2));

        Assert.Single(snapshot.Entries);
        Assert.Equal(cycle2, snapshot.Entries[("lucerne", IconD2)].Cycle);
        Assert.True(snapshot.HasChanged);
    }

    [Fact(Timeout = 5000)]
    public void Identical_cycle_does_not_set_has_changed()
    {
        var cycle = new CycleId(T0);
        var snapshot = ModelSnapshot.Empty
            .Update(Forecast("lucerne", IconD2, cycle))
            .Update(Forecast("lucerne", IconD2, cycle));

        Assert.False(snapshot.HasChanged);
    }

    [Fact(Timeout = 5000)]
    public void Different_models_coexist()
    {
        var cycle = new CycleId(T0);
        var snapshot = ModelSnapshot.Empty
            .Update(Forecast("lucerne", IconD2, cycle))
            .Update(Forecast("lucerne", Ecmwf, cycle));

        Assert.Equal(2, snapshot.Entries.Count);
    }

    [Fact(Timeout = 5000)]
    public void Original_snapshot_is_unchanged_after_update()
    {
        var cycle = new CycleId(T0);
        var original = ModelSnapshot.Empty;
        _ = original.Update(Forecast("lucerne", IconD2, cycle));

        Assert.Empty(original.Entries);
    }

    [Fact(Timeout = 5000)]
    public void Models_for_returns_models_at_location()
    {
        var cycle = new CycleId(T0);
        var snapshot = ModelSnapshot.Empty
            .Update(Forecast("lucerne", IconD2, cycle))
            .Update(Forecast("lucerne", Ecmwf, cycle))
            .Update(Forecast("zurich", IconD2, cycle));

        var lucerneModels = snapshot.ModelsFor("lucerne");
        Assert.Equal(2, lucerneModels.Count);
        Assert.Contains(IconD2, lucerneModels);
        Assert.Contains(Ecmwf, lucerneModels);

        var zurichModels = snapshot.ModelsFor("zurich");
        Assert.Single(zurichModels);
        Assert.Contains(IconD2, zurichModels);
    }

    [Fact(Timeout = 5000)]
    public void Models_for_unknown_location_returns_empty()
    {
        Assert.Empty(ModelSnapshot.Empty.ModelsFor("unknown"));
    }
}
