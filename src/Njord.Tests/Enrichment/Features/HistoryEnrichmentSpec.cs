using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Enrichment.Features;
using Njord.Mqtt;

namespace Njord.Tests.Enrichment.Features;

public sealed class HistoryEnrichmentSpec
{
    private static HistoryEnrichment CreateFeature(bool enabled = true)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var enrichment = new EnrichmentOptions
        {
            History = new HistoryOptions { Enabled = enabled },
        };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return new HistoryEnrichment(
            Options.Create(options), Options.Create(enrichment), parameters, TimeProvider.System);
    }

    [Fact(Timeout = 5000)]
    public void Implements_IActorEnrichment()
    {
        var feature = CreateFeature();

        Assert.IsAssignableFrom<IActorEnrichment>(feature);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options()
    {
        Assert.True(CreateFeature(enabled: true).Enabled);
        Assert.False(CreateFeature(enabled: false).Enabled);
    }

    [Fact(Timeout = 5000)]
    public void TypeName_is_history()
    {
        Assert.Equal("history", CreateFeature().TypeName);
    }

    [Fact(Timeout = 5000)]
    public void BuildDiscoveryPayload_returns_valid_json()
    {
        var feature = CreateFeature();
        var ctx = new DiscoveryContext(new MqttOptions(), TimeSpan.FromMinutes(60), "1.0.0");

        var payload = feature.BuildDiscoveryPayload(ctx, "lucerne");
        var json = JsonNode.Parse(payload);

        Assert.NotNull(json);
        Assert.NotNull(json!["dev"]);
        Assert.NotNull(json["cmps"]);
    }
}
