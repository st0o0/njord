using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcConsumerEntityStateTests
{
    private static readonly DateTimeOffset _anchor = TestTimeConstants.BudgetEpoch;
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    [Fact]
    public void Feature_result_maps_to_the_entity_id_of_its_location_and_feature()
    {
        var registry = Registry([_vienna], new EntityTestRule("Heat"));
        var alert = EnrichmentEvents.Alerts(_vienna, _anchor);

        var state = GrpcConsumerState.Empty.ApplyEntity(registry.Find(alert)!, alert, _anchor);

        var entity = Assert.Single(state.EntityStates);
        Assert.Multiple(
            () => Assert.Equal("vienna_heat", entity.Key),
            () => Assert.Equal(alert, entity.Value.Result),
            () => Assert.Equal(_anchor, entity.Value.LastUpdated));
    }

    [Fact]
    public void Result_type_without_an_entity_is_not_in_the_lookup()
    {
        var registry = Registry([_vienna], new EntityTestRule("Frost"));

        Assert.Null(registry.Find(EnrichmentEvents.Alerts(_vienna, _anchor)));
    }

    [Fact]
    public void Disabled_features_have_no_entity()
    {
        var registry = Registry([_vienna], new EntityTestRule("Heat", enabled: false));

        Assert.Null(registry.Find(EnrichmentEvents.Alerts(_vienna, _anchor)));
    }

    [Fact]
    public void Locations_keep_separate_entity_states()
    {
        var registry = Registry([_vienna, DomainData.Lucerne], new EntityTestRule("Heat"));
        var viennaAlert = EnrichmentEvents.Alerts(_vienna, _anchor);
        var lucerneAlert = EnrichmentEvents.Alerts(DomainData.Lucerne, _anchor);

        var state = GrpcConsumerState.Empty
            .ApplyEntity(registry.Find(viennaAlert)!, viennaAlert, _anchor)
            .ApplyEntity(registry.Find(lucerneAlert)!, lucerneAlert, _anchor);

        Assert.Equal(["lucerne_heat", "vienna_heat"], state.EntityStates.Keys.Order());
    }

    private static EntityRegistry Registry(Location[] locations, params EntityTestRule[] rules) =>
        new(rules, Options.Create(new NjordOptions { ResolvedLocations = locations }), Options.Create(new ActiveModels()));
}
