using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Mqtt;
using Njord.Mqtt.Presentation;

namespace Njord.Mqtt.Tests.Presentation;

public sealed class HistoryPresenterSpec
{
    [Fact]
    public void BuildDiscoveryPayload_returns_valid_json()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var presenter = new HistoryPresenter(Options.Create(options));
        var ctx = new DiscoveryContext(new MqttOptions(), TimeSpan.FromMinutes(60), "1.0.0");

        var payload = presenter.BuildDiscoveryPayload(ctx, "lucerne");
        var json = JsonNode.Parse(payload);

        Assert.NotNull(json);
        Assert.NotNull(json["dev"]);
        Assert.NotNull(json["cmps"]);
    }

    [Fact]
    public void Enabled_reflects_options()
    {
        var enabled = new NjordOptions { Enrichment = new EnrichmentOptions { History = new HistoryOptions { Enabled = true } } };
        var disabled = new NjordOptions { Enrichment = new EnrichmentOptions { History = new HistoryOptions { Enabled = false } } };

        Assert.True(new HistoryPresenter(Options.Create(enabled)).Enabled);
        Assert.False(new HistoryPresenter(Options.Create(disabled)).Enabled);
    }
}
