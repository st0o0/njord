using Njord.Core;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerStateExtensionsTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly DateTimeOffset _anchor = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void ApplyEntity_adds_new_entity_state()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);

        var state = GrpcConsumerState.Empty.ApplyEntity("vienna_heat", result, _anchor);

        var entity = Assert.Single(state.EntityStates);
        Assert.Multiple(
            () => Assert.Equal("vienna_heat", entity.Key),
            () => Assert.Same(result, entity.Value.Result),
            () => Assert.Equal(_anchor, entity.Value.LastUpdated),
            () => Assert.True(entity.Value.IsAvailable));
    }

    [Fact]
    public void ApplyEntity_replaces_existing_entity()
    {
        var first = EnrichmentEvents.Alerts(_vienna, _anchor);
        var second = EnrichmentEvents.Alerts(_vienna, _anchor.AddHours(1));
        var later = _anchor.AddHours(1);

        var state = GrpcConsumerState.Empty
            .ApplyEntity("vienna_heat", first, _anchor)
            .ApplyEntity("vienna_heat", second, later);

        var entity = Assert.Single(state.EntityStates);
        Assert.Multiple(
            () => Assert.Same(second, entity.Value.Result),
            () => Assert.Equal(later, entity.Value.LastUpdated));
    }

    [Fact]
    public void CheckAvailability_orders_unavailable_and_available_by_entity_id()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);
        var threshold = TimeSpan.FromMinutes(10);
        var state = GrpcConsumerState.Empty
            .ApplyEntity("z_entity", result, _anchor)
            .ApplyEntity("a_entity", result, _anchor);

        var (_, unavailable, _) = state.CheckAvailability(
            _anchor + threshold + TimeSpan.FromSeconds(1), threshold);

        Assert.Equal(["a_entity", "z_entity"], unavailable);
    }

    [Fact]
    public void CheckAvailability_sets_is_available_false_on_stale_entities()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);
        var threshold = TimeSpan.FromMinutes(10);
        var state = GrpcConsumerState.Empty.ApplyEntity("vienna_heat", result, _anchor);

        var (next, _, _) = state.CheckAvailability(
            _anchor + threshold + TimeSpan.FromSeconds(1), threshold);

        Assert.Multiple(
            () => Assert.False(next.EntityStates["vienna_heat"].IsAvailable),
            () => Assert.Contains("vienna_heat", next.StaleEntities));
    }

    [Fact]
    public void CheckAvailability_recovers_previously_stale_entities_in_order()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);
        var threshold = TimeSpan.FromMinutes(10);
        var late = _anchor + threshold + TimeSpan.FromMinutes(1);
        var (stale, _, _) = GrpcConsumerState.Empty
            .ApplyEntity("z_entity", result, _anchor)
            .ApplyEntity("a_entity", result, _anchor)
            .CheckAvailability(late, threshold);

        var refreshed = stale
            .ApplyEntity("z_entity", result, late)
            .ApplyEntity("a_entity", result, late);
        var (next, unavailable, available) = refreshed.CheckAvailability(late, threshold);

        Assert.Multiple(
            () => Assert.Empty(unavailable),
            () => Assert.Equal(["a_entity", "z_entity"], available),
            () => Assert.True(next.EntityStates["a_entity"].IsAvailable),
            () => Assert.True(next.EntityStates["z_entity"].IsAvailable),
            () => Assert.Empty(next.StaleEntities));
    }

    [Fact]
    public void CheckAvailability_at_exact_threshold_stays_available()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);
        var threshold = TimeSpan.FromMinutes(10);

        var (next, unavailable, _) = GrpcConsumerState.Empty
            .ApplyEntity("vienna_heat", result, _anchor)
            .CheckAvailability(_anchor + threshold, threshold);

        Assert.Multiple(
            () => Assert.Empty(unavailable),
            () => Assert.True(next.EntityStates["vienna_heat"].IsAvailable));
    }

    [Fact]
    public void CheckAvailability_mixed_stale_and_fresh_sets_availability_per_entity()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);
        var threshold = TimeSpan.FromMinutes(10);
        var late = _anchor + threshold + TimeSpan.FromMinutes(1);
        var state = GrpcConsumerState.Empty
            .ApplyEntity("stale_entity", result, _anchor)
            .ApplyEntity("fresh_entity", result, late);

        var (next, unavailable, _) = state.CheckAvailability(late, threshold);

        Assert.Multiple(
            () => Assert.Equal(["stale_entity"], unavailable),
            () => Assert.False(next.EntityStates["stale_entity"].IsAvailable),
            () => Assert.True(next.EntityStates["fresh_entity"].IsAvailable));
    }

    [Fact]
    public void CheckAvailability_does_not_report_already_stale_entity_twice()
    {
        var result = EnrichmentEvents.Alerts(_vienna, _anchor);
        var threshold = TimeSpan.FromMinutes(10);
        var late = _anchor + threshold + TimeSpan.FromMinutes(1);
        var (stale, _, _) = GrpcConsumerState.Empty
            .ApplyEntity("vienna_heat", result, _anchor)
            .CheckAvailability(late, threshold);

        var (_, unavailable, _) = stale.CheckAvailability(late + TimeSpan.FromMinutes(1), threshold);

        Assert.Empty(unavailable);
    }
}
