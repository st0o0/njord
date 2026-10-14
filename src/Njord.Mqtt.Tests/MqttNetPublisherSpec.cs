using MQTTnet;
using Njord.Mqtt.Transport;

namespace Njord.Mqtt.Tests;

public sealed class MqttNetPublisherSpec
{
    [Fact]
    public void A_refused_connect_result_throws_naming_the_result_code()
    {
        var result = new MqttClientConnectResult
        {
            ResultCode = MqttClientConnectResultCode.BadUserNameOrPassword,
            ReasonString = "bad credentials",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => MqttNetPublisher.EnsureAccepted(result));

        Assert.Multiple(
            () => Assert.Contains("BadUserNameOrPassword", ex.Message),
            () => Assert.Contains("bad credentials", ex.Message));
    }

    [Fact]
    public void An_accepted_connect_result_passes()
    {
        var result = new MqttClientConnectResult { ResultCode = MqttClientConnectResultCode.Success };

        var ex = Record.Exception(() => MqttNetPublisher.EnsureAccepted(result));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Only_the_loss_of_an_established_connection_is_reported(bool clientWasConnected, bool expected)
    {
        var args = new MqttClientDisconnectedEventArgs(
            clientWasConnected, null, MqttClientDisconnectReason.UnspecifiedError, null, [], null);

        Assert.Equal(expected, MqttNetPublisher.IsLostConnection(args));
    }
}
