using Njord.IntegrationTests.Infrastructure;

namespace Njord.IntegrationTests;

[Collection("E2E")]
public sealed class HaBirthRediscoverySpec
{
    private readonly E2EFixture _fixture;

    public HaBirthRediscoverySpec(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 60_000, Skip = "Requires Docker")]
    public async Task Ha_birth_triggers_rediscovery_of_all_devices()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var discoveryBefore = _fixture.Mosquitto.Messages
            .Count(m => m.Topic.StartsWith("homeassistant/device/", StringComparison.Ordinal));

        _fixture.Mosquitto.ClearMessages();

        // TODO: publish "online" to homeassistant/status via a separate MQTT client
        // then wait for re-published discovery messages
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        var discoveryAfter = _fixture.Mosquitto.Messages
            .Count(m => m.Topic.StartsWith("homeassistant/device/", StringComparison.Ordinal));

        Assert.True(discoveryAfter > 0);
    }
}
