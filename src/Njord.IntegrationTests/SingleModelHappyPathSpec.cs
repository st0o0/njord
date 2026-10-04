using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests;

[Collection("E2E")]
public sealed class SingleModelHappyPathSpec
{
    private readonly E2EFixture _fixture;

    public SingleModelHappyPathSpec(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 60_000, Skip = "Requires Docker")]
    public async Task Single_model_poll_produces_mqtt_discovery_and_state()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var messages = _fixture.Mosquitto.Messages
            .Where(m => m.Topic.StartsWith("homeassistant/", StringComparison.Ordinal)
                        || m.Topic.StartsWith("njord/", StringComparison.Ordinal))
            .OrderBy(m => m.Topic)
            .Select(m => new { m.Topic, m.Payload, m.Retain })
            .ToList();

        Assert.NotEmpty(messages);
    }
}

[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>;
