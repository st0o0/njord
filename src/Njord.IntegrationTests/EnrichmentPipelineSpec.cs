using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests;

[Collection("E2E")]
public sealed class EnrichmentPipelineSpec
{
    private readonly E2EFixture _fixture;

    public EnrichmentPipelineSpec(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 60_000, Skip = "Requires Docker")]
    public async Task Poll_cycle_produces_enrichment_payloads()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var enrichmentMessages = _fixture.Mosquitto.Messages
            .Where(m => m.Topic.Contains("njord_lucerne_", StringComparison.Ordinal)
                        && !m.Topic.Contains("icon_d2", StringComparison.Ordinal))
            .OrderBy(m => m.Topic)
            .ToList();

        Assert.NotEmpty(enrichmentMessages);
    }
}
