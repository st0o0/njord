using System.Text.Json.Nodes;
using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class LegacyConfigKeysSpec
{
    [Fact]
    public void Every_old_key_is_moved_into_its_per_concern_section()
    {
        var root = JsonNode.Parse("""
            {
              "Njord": {
                "PollInterval": "00:45:00",
                "DiscoveryInterval": "00:10:00",
                "BudgetOverride": { "RequestsPerMonth": 100000, "RequestsPerMinute": 200 },
                "OpenMeteoBaseUrl": "http://localhost:8080",
                "PersistencePath": "data/other.db",
                "ForecastDays": 7
              }
            }
            """)!.AsObject();

        var migrated = LegacyConfigKeys.Migrate(root);

        Assert.Equal(
            JsonNode.Parse("""
            {
              "Njord": {
                "ForecastDays": 7,
                "Polling": {
                  "PollInterval": "00:45:00",
                  "DiscoveryInterval": "00:10:00",
                  "BudgetOverride": { "RequestsPerMonth": 100000, "RequestsPerMinute": 200 }
                },
                "OpenMeteo": { "BaseUrl": "http://localhost:8080" },
                "Storage": { "PersistencePath": "data/other.db" }
              }
            }
            """)!.ToJsonString(),
            root.ToJsonString());
        Assert.Equal(
            [
                "Njord:PollInterval", "Njord:DiscoveryInterval", "Njord:BudgetOverride",
                "Njord:OpenMeteoBaseUrl", "Njord:PersistencePath",
            ],
            migrated);
    }

    [Fact]
    public void A_file_with_the_new_shape_is_left_untouched()
    {
        var root = JsonNode.Parse("""{ "Njord": { "Polling": { "PollInterval": "00:45:00" } } }""")!.AsObject();

        var migrated = LegacyConfigKeys.Migrate(root);

        Assert.Empty(migrated);
        Assert.Equal("""{"Njord":{"Polling":{"PollInterval":"00:45:00"}}}""", root.ToJsonString());
    }

    [Fact]
    public void A_new_key_wins_over_its_old_spelling_in_a_mixed_file()
    {
        var root = JsonNode.Parse("""
            { "Njord": { "PollInterval": "00:45:00", "Polling": { "PollInterval": "02:00:00" } } }
            """)!.AsObject();

        var migrated = LegacyConfigKeys.Migrate(root);

        Assert.Equal(["Njord:PollInterval"], migrated);
        Assert.Equal("""{"Njord":{"Polling":{"PollInterval":"02:00:00"}}}""", root.ToJsonString());
    }

    [Fact]
    public void Key_names_are_matched_case_insensitively()
    {
        var root = JsonNode.Parse("""{ "njord": { "pollinterval": "00:45:00" } }""")!.AsObject();

        LegacyConfigKeys.Migrate(root);

        Assert.Equal("""{"njord":{"Polling":{"PollInterval":"00:45:00"}}}""", root.ToJsonString());
    }
}
