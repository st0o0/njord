using Njord.Compute.Analysis;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Enrichment.Tests;

public sealed class ForecastHistoryStateSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.ForecastEpoch;

    private static ForecastRecord MakeRecord(DateTimeOffset? timestamp = null, string location = "lucerne") =>
        new(timestamp ?? T0, location,
            new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>(),
            new Dictionary<string, double?> { ["temperature_2m"] = 22.5 });

    [Fact]
    public void Empty_state_has_no_records()
    {
        var state = ForecastHistoryState.Create(30, 10);
        Assert.Empty(state.History.Records);
        Assert.Equal(0, state.EventsSinceSnapshot);
    }

    [Fact]
    public void Apply_adds_record_to_history()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        Assert.Single(state.History.Records);
    }

    [Fact]
    public void Apply_increments_event_counter()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        Assert.Equal(1, state.EventsSinceSnapshot);

        state = state.Apply(MakeRecord(T0.AddHours(1)));
        Assert.Equal(2, state.EventsSinceSnapshot);
    }

    [Fact]
    public void ApplyRecover_skips_events_before_cutoff()
    {
        var state = ForecastHistoryState.Create(30, 10);
        var oldRecord = MakeRecord(T0.AddDays(-31));
        var cutoff = T0.AddDays(-30);

        state = state.ApplyRecover(oldRecord, cutoff);
        Assert.Empty(state.History.Records);
    }

    [Fact]
    public void ApplyRecover_keeps_events_after_cutoff()
    {
        var state = ForecastHistoryState.Create(30, 10);
        var recentRecord = MakeRecord(T0);
        var cutoff = T0.AddDays(-30);

        state = state.ApplyRecover(recentRecord, cutoff);
        Assert.Single(state.History.Records);
    }

    [Fact]
    public void ResetSnapshotCounter_zeroes_counter()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        state = state.Apply(MakeRecord(T0.AddHours(1)));
        Assert.Equal(2, state.EventsSinceSnapshot);

        state = state.ResetSnapshotCounter();
        Assert.Equal(0, state.EventsSinceSnapshot);
    }

    [Fact]
    public void GetSnapshot_returns_history_result()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());

        var snapshot = state.GetSnapshot();
        Assert.IsType<QueryHistoryCompleted>(snapshot);
        Assert.Single(snapshot.History.Records);
    }

    [Fact]
    public void GetPersistenceState_returns_valid_dto()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());

        var dto = state.GetPersistenceState();
        Assert.Equal(30, dto.RetentionDays);
        Assert.Single(dto.Records);
        Assert.Equal(T0.UtcTicks, dto.Records[0].TimestampUtcTicks);
    }

    [Fact]
    public void FromPersistence_roundtrip_preserves_state()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        state = state.Apply(MakeRecord(T0.AddHours(1)));

        var dto = state.GetPersistenceState();
        var restored = ForecastHistoryStateExtensions.FromPersistence(dto, 10);

        Assert.Equal(state.History.Records.Count, restored.History.Records.Count);
        Assert.Equal(0, restored.EventsSinceSnapshot);
        Assert.Equal(10, restored.SnapshotInterval);
    }

    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private static ModelForecast MakeForecast(string model, string location, double temperatureAtT0) =>
        new(new WeatherModel(model), location, new CycleId(T0),
            new ForecastSeries(
            [
                new ForecastPoint(T0, new Dictionary<ParameterDef, double?> { [Temperature] = temperatureAtT0 }),
                new ForecastPoint(T0.AddHours(6), new Dictionary<ParameterDef, double?> { [Temperature] = 100.0 }),
            ]),
            DailyForecastSeries.Empty);

    [Fact]
    public void ProcessSnapshot_uses_the_point_nearest_to_now_and_the_median_across_models()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeForecast("icon_d2", "lucerne", 10.0))
            .Update(MakeForecast("icon_eu", "lucerne", 20.0))
            .Update(MakeForecast("gfs_seamless", "lucerne", 40.0));

        var evt = ForecastHistoryState.Create(30, 10).ProcessSnapshot(snapshot, "lucerne", Parameters, T0.AddMinutes(10));

        Assert.Equal(T0.AddMinutes(10), evt.Timestamp);
        Assert.Equal("lucerne", evt.Location);
        Assert.Equal(20.0, evt.ConsensusValues["temperature_2m"]);
    }

    [Fact]
    public void ProcessSnapshot_computes_the_median_from_partial_model_coverage()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeForecast("icon_d2", "lucerne", 10.0))
            .Update(MakeForecast("icon_eu", "lucerne", 20.0));

        var evt = ForecastHistoryState.Create(30, 10).ProcessSnapshot(snapshot, "lucerne", Parameters, T0);

        Assert.Equal(15.0, evt.ConsensusValues["temperature_2m"]);
    }

    [Fact]
    public void ProcessSnapshot_ignores_entries_of_other_locations()
    {
        var snapshot = ModelSnapshot.Empty
            .Update(MakeForecast("icon_d2", "lucerne", 10.0))
            .Update(MakeForecast("icon_d2", "zurich", 30.0));

        var evt = ForecastHistoryState.Create(30, 10).ProcessSnapshot(snapshot, "lucerne", Parameters, T0);

        Assert.Equal(10.0, evt.ConsensusValues["temperature_2m"]);
    }

    [Fact]
    public void ProcessSnapshot_without_matching_entries_yields_an_empty_consensus()
    {
        var snapshot = ModelSnapshot.Empty.Update(MakeForecast("icon_d2", "zurich", 30.0));

        var evt = ForecastHistoryState.Create(30, 10).ProcessSnapshot(snapshot, "lucerne", Parameters, T0);

        Assert.Equal("lucerne", evt.Location);
        Assert.Empty(evt.ConsensusValues);
    }
}
