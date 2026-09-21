using Njord.Domain.Analysis;
using Njord.Grpc;
using Njord.Persistence;

namespace Njord.Tests.Grpc;

public sealed class EnrichmentSnapshotStateSpec
{
    private static readonly AlertResult TestAlert = new("lucerne", []);
    private static readonly IndexResult TestIndex = new("lucerne",
        [new DayScoreSet(0, 80, 90, 70, 85, 95, 60, 88, 75, HoursIncluded: 14)], null, null);

    [Fact(Timeout = 5000)]
    public void Empty_state_has_no_enrichments()
    {
        var state = EnrichmentSnapshotState.Empty;

        Assert.Empty(state.Enrichments);
        Assert.Equal(0, state.UpdatesSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void Apply_adds_enrichment_and_increments_counter()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert);

        Assert.Single(state.Enrichments);
        Assert.Equal(1, state.UpdatesSinceSnapshot);
        Assert.Same(TestAlert, state.Enrichments["lucerne|alerts"]);
    }

    [Fact(Timeout = 5000)]
    public void Apply_overwrites_existing_enrichment()
    {
        var updated = new AlertResult("lucerne", [Alert.None(AlertType.Heat)]);
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert)
            .Apply("lucerne|alerts", updated);

        Assert.Single(state.Enrichments);
        Assert.Equal(2, state.UpdatesSinceSnapshot);
        Assert.Same(updated, state.Enrichments["lucerne|alerts"]);
    }

    [Fact(Timeout = 5000)]
    public void GetEnrichment_returns_found_for_existing_key()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert);

        var response = state.GetEnrichment("lucerne|alerts");

        var found = Assert.IsType<EnrichmentFound>(response);
        Assert.Same(TestAlert, found.Result);
    }

    [Fact(Timeout = 5000)]
    public void GetEnrichment_returns_not_found_for_missing_key()
    {
        var state = EnrichmentSnapshotState.Empty;

        var response = state.GetEnrichment("lucerne|unknown");

        Assert.IsType<EnrichmentNotFound>(response);
    }

    [Fact(Timeout = 5000)]
    public void GetAllEnrichments_filters_by_location()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert)
            .Apply("lucerne|indices", TestIndex)
            .Apply("zurich|alerts", new AlertResult("zurich", []));

        var result = state.GetAllEnrichments("lucerne");

        Assert.Equal(2, result.Results.Count);
        Assert.Contains(result.Results, r => r.TypeName == "alerts");
        Assert.Contains(result.Results, r => r.TypeName == "indices");
    }

    [Fact(Timeout = 5000)]
    public void GetAllEnrichments_returns_empty_for_unknown_location()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert);

        var result = state.GetAllEnrichments("zurich");

        Assert.Empty(result.Results);
    }

    [Fact(Timeout = 5000)]
    public void ResetSnapshotCounter_clears_counter()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert)
            .Apply("lucerne|indices", TestIndex);

        Assert.Equal(2, state.UpdatesSinceSnapshot);

        var reset = state.ResetSnapshotCounter();
        Assert.Equal(0, reset.UpdatesSinceSnapshot);
        Assert.Equal(2, reset.Enrichments.Count);
    }

    [Fact(Timeout = 5000)]
    public void GetPersistenceState_returns_valid_dto()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert);

        var dto = state.GetPersistenceState();

        Assert.IsType<EnrichmentSnapshotDto>(dto);
        Assert.Single(dto.Enrichments);
    }

    [Fact(Timeout = 5000)]
    public void FromPersistence_roundtrip_preserves_state()
    {
        var state = EnrichmentSnapshotState.Empty
            .Apply("lucerne|alerts", TestAlert);

        var dto = state.GetPersistenceState();
        var restored = EnrichmentSnapshotStateExtensions.FromPersistence(dto);

        Assert.Single(restored.Enrichments);
        Assert.Equal(0, restored.UpdatesSinceSnapshot);
        Assert.IsType<AlertResult>(restored.Enrichments["lucerne|alerts"]);
    }
}
