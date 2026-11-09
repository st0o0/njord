using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Results;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class EntityIdLookupTests
{
    private static readonly DateTimeOffset _anchor = TestTimeConstants.BudgetEpoch;
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    [Fact]
    public void Observation_result_maps_to_the_entity_id_of_its_feature_name()
    {
        var lookup = Lookup([_vienna], new EntityTestRule("Pm25Observation", OutputKind.Observation));

        Assert.Equal("vienna_pm25_observation", lookup.Find(new ObservationResult(_vienna, "Pm25Observation", 42.5)));
    }

    [Fact]
    public void Observation_features_of_one_location_get_distinct_entity_ids()
    {
        var lookup = Lookup(
            [_vienna],
            new EntityTestRule("Pm25Observation", OutputKind.Observation),
            new EntityTestRule("UvIndexObservation", OutputKind.Observation));

        Assert.Multiple(
            () => Assert.Equal("vienna_pm25_observation", lookup.Find(new ObservationResult(_vienna, "Pm25Observation", 1))),
            () => Assert.Equal("vienna_uv_index_observation", lookup.Find(new ObservationResult(_vienna, "UvIndexObservation", 2))),
            () => Assert.Equal(2, lookup.Count));
    }

    [Fact]
    public void Observation_of_a_disabled_or_unknown_feature_has_no_entity()
    {
        var lookup = Lookup(
            [_vienna],
            new EntityTestRule("Pm25Observation", OutputKind.Observation, enabled: false));

        Assert.Multiple(
            () => Assert.Null(lookup.Find(new ObservationResult(_vienna, "Pm25Observation", 1))),
            () => Assert.Null(lookup.Find(new ObservationResult(_vienna, "Unknown", 1))));
    }

    [Fact]
    public void Alerts_still_resolve_by_result_type_next_to_observations()
    {
        var lookup = Lookup(
            [_vienna],
            new EntityTestRule("Heat"),
            new EntityTestRule("Pm25Observation", OutputKind.Observation));

        Assert.Multiple(
            () => Assert.Equal("vienna_heat", lookup.Find(EnrichmentEvents.Alerts(_vienna, _anchor))),
            () => Assert.Equal("vienna_pm25_observation", lookup.Find(new ObservationResult(_vienna, "Pm25Observation", 3))));
    }

    private static EntityIdLookup Lookup(Location[] locations, params EntityTestRule[] rules) =>
        new(rules, new NjordOptions { ResolvedLocations = locations });
}
