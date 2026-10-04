using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests;

[Collection("E2E")]
public sealed class MultiModelConsensusSpec
{
    private readonly E2EFixture _fixture;

    public MultiModelConsensusSpec(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 60_000, Skip = "Requires Docker")]
    public async Task Two_models_produce_consensus_device_payloads()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var consensusMessages = _fixture.Mosquitto.Messages
            .Where(m => m.Topic.Contains("consensus", StringComparison.Ordinal))
            .OrderBy(m => m.Topic)
            .ToList();

        Assert.NotEmpty(consensusMessages);
    }
}
