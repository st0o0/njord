using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests;

[Collection("E2E")]
public sealed class SensorPushIntegrationSpec
{
    private readonly E2EFixture _fixture;

    public SensorPushIntegrationSpec(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 60_000, Skip = "Requires Docker")]
    public async Task Sensor_push_value_reflected_in_enrichment()
    {
        // TODO: push indoor temperature via gRPC, trigger poll, verify enrichment uses it
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.NotNull(_fixture.System);
    }
}
