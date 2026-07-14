using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment.Features;
using Njord.Mqtt;

namespace Njord.Tests.Enrichment.Features;

public sealed class ConsensusEnrichmentSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static ConsensusEnrichment CreateFeature(bool enabled = true)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var enrichment = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = enabled },
        };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return new ConsensusEnrichment(
            Options.Create(options), Options.Create(enrichment), parameters, TimeProvider.System);
    }

    private static ModelSnapshot MakeSnapshot()
    {
        var points = Enumerable.Range(0, 24).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?> { [Temperature] = 20.0 + h }))
            .ToList();
        var forecast = new ModelForecast(new("icon_d2"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        return ModelSnapshot.Empty.Update(forecast);
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_one_enrichment_update_per_location()
    {
        var feature = CreateFeature();
        var snapshot = MakeSnapshot();

        var events = feature.Compute(snapshot, ["lucerne"]).ToList();

        Assert.Single(events);
        var update = Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]);
        Assert.Equal("lucerne", update.Location);
        Assert.Equal("consensus", update.TypeName);
        Assert.IsType<ConsensusResult>(update.Result);
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_events_for_multiple_locations()
    {
        var feature = CreateFeature();
        var snapshot = MakeSnapshot();

        var events = feature.Compute(snapshot, ["lucerne", "zurich"]).ToList();

        Assert.Equal(2, events.Count);
    }

    [Fact(Timeout = 5000)]
    public void BuildDiscoveryPayload_returns_valid_json_with_device_envelope()
    {
        var feature = CreateFeature();
        var ctx = new DiscoveryContext(new MqttOptions(), TimeSpan.FromMinutes(60), "1.0.0");

        var payload = feature.BuildDiscoveryPayload(ctx, "lucerne");
        var json = JsonNode.Parse(payload);

        Assert.NotNull(json);
        Assert.NotNull(json!["dev"]);
        Assert.NotNull(json["cmps"]);
        Assert.Equal("njord", json["dev"]!["mf"]!.GetValue<string>());
    }

    [Fact(Timeout = 5000)]
    public void ToStateMessages_returns_mqtt_messages()
    {
        var feature = CreateFeature();
        var result = new ConsensusResult([]);

        var messages = feature.ToStateMessages(result, "njord", "lucerne");

        Assert.NotNull(messages);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options()
    {
        Assert.True(CreateFeature(enabled: true).Enabled);
        Assert.False(CreateFeature(enabled: false).Enabled);
    }
}
