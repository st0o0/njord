using System.Text.Json.Nodes;
using Njord.Grpc.V1;
using Njord.Tests.Shared;

namespace Njord.Tests.Integration.E2E;

[Collection("NjordAppHost")]
public sealed class EndToEndPipelineSpec
{
    private readonly NjordAppHostFixture _fixture;

    public EndToEndPipelineSpec(NjordAppHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 120000)]
    public async Task Full_pipeline_from_api_fetch_to_mqtt_retained_messages()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.WireMockAdmin.ResetMappingsAsync();
        await _fixture.WireMockAdmin.PostMappingAsync(new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel { Matchers = [new() { Name = "WildcardMatcher", Pattern = "/v1/forecast" }] },
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Body = FixtureReader.Read("openmeteo-icon_eu-96h.json"),
                Headers = new Dictionary<string, object> { ["Content-Type"] = "application/json" },
            },
        });

        var configClient = new ConfigService.ConfigServiceClient(_fixture.GrpcChannel);
        var config = await configClient.GetConfigAsync(new GetConfigRequest(), cancellationToken: ct);
        Assert.True(config.Locations.Count > 0, "Njord should have at least one location configured");

        var triggerResponse = await configClient.TriggerPollAsync(new TriggerPollRequest(), cancellationToken: ct);
        Assert.True(triggerResponse.TriggeredCount > 0, "TriggerPoll should have triggered at least one poll");

        await Task.Delay(TimeSpan.FromSeconds(15), ct);

        var retained = await MosquittoHelper.CollectRetainedAsync(
            _fixture.MqttOptions,
            ["njord/#", "homeassistant/device/+/config"],
            ct);

        Assert.True(retained.ContainsKey("njord/status"), "njord/status topic should exist");

        var discoveryTopics = retained.Keys.Where(k => k.StartsWith("homeassistant/device/njord_")).ToList();
        Assert.True(discoveryTopics.Count > 0,
            $"Should have at least one discovery config. Topics: {string.Join(", ", retained.Keys.Take(20))}");

        var firstDiscovery = discoveryTopics[0];
        var deviceConfig = JsonNode.Parse(retained[firstDiscovery])!;
        var components = deviceConfig["cmps"]!.AsObject().Count;
        Assert.True(components > 0, $"Device should have components but had {components}");

        var stateTopics = retained.Keys.Where(k => k.StartsWith("njord/") && k != "njord/status" && k.Count(c => c == '/') >= 2).ToList();
        Assert.True(stateTopics.Count > 0,
            $"Should have state topics. njord topics: {string.Join(", ", retained.Keys.Where(k => k.StartsWith("njord/")).Take(20))}");
    }
}
