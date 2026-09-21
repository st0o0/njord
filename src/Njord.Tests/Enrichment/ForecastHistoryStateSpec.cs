using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Persistence;

namespace Njord.Tests.Enrichment;

public sealed class ForecastHistoryStateSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static ForecastRecord MakeRecord(DateTimeOffset? timestamp = null, string location = "lucerne") =>
        new(timestamp ?? T0, location,
            new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>(),
            new Dictionary<string, double?> { ["temperature_2m"] = 22.5 });

    [Fact(Timeout = 5000)]
    public void Empty_state_has_no_records()
    {
        var state = ForecastHistoryState.Create(30, 10);
        Assert.Empty(state.History.Records);
        Assert.Equal(0, state.EventsSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void Apply_adds_record_to_history()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        Assert.Single(state.History.Records);
    }

    [Fact(Timeout = 5000)]
    public void Apply_increments_event_counter()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        Assert.Equal(1, state.EventsSinceSnapshot);

        state = state.Apply(MakeRecord(T0.AddHours(1)));
        Assert.Equal(2, state.EventsSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void ApplyRecover_skips_events_before_cutoff()
    {
        var state = ForecastHistoryState.Create(30, 10);
        var oldRecord = MakeRecord(T0.AddDays(-31));
        var cutoff = T0.AddDays(-30);

        state = state.ApplyRecover(oldRecord, cutoff);
        Assert.Empty(state.History.Records);
    }

    [Fact(Timeout = 5000)]
    public void ApplyRecover_keeps_events_after_cutoff()
    {
        var state = ForecastHistoryState.Create(30, 10);
        var recentRecord = MakeRecord(T0);
        var cutoff = T0.AddDays(-30);

        state = state.ApplyRecover(recentRecord, cutoff);
        Assert.Single(state.History.Records);
    }

    [Fact(Timeout = 5000)]
    public void ResetSnapshotCounter_zeroes_counter()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());
        state = state.Apply(MakeRecord(T0.AddHours(1)));
        Assert.Equal(2, state.EventsSinceSnapshot);

        state = state.ResetSnapshotCounter();
        Assert.Equal(0, state.EventsSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void GetSnapshot_returns_history_result()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());

        var snapshot = state.GetSnapshot();
        Assert.IsType<ForecastHistoryResult>(snapshot);
        Assert.Single(snapshot.History.Records);
    }

    [Fact(Timeout = 5000)]
    public void GetPersistenceState_returns_valid_dto()
    {
        var state = ForecastHistoryState.Create(30, 10);
        state = state.Apply(MakeRecord());

        var dto = state.GetPersistenceState();
        Assert.Equal(30, dto.RetentionDays);
        Assert.Single(dto.Records);
        Assert.Equal(T0.UtcTicks, dto.Records[0].TimestampUtcTicks);
    }

    [Fact(Timeout = 5000)]
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
}
