using System.Net;
using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using Njord.Core.Configuration.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

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

    private static MqttOptions Options(Action<MqttOptions>? configure = null) =>
        AppSettingsDefaults.Options<MqttOptions>(o =>
        {
            o.Host = "broker.local";
            o.Port = 1884;
            o.BaseTopic = "njord";
            o.Username = null;
            o.Password = null;
            configure?.Invoke(o);
        });

    [Fact]
    public void Client_options_target_the_configured_broker()
    {
        var client = MqttNetPublisher.BuildClientOptions(Options());

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.IsType<MqttClientTcpOptions>(client.ChannelOptions).RemoteEndpoint);
        Assert.Multiple(
            () => Assert.Equal("broker.local", endpoint.Host),
            () => Assert.Equal(1884, endpoint.Port));
    }

    [Fact]
    public void Client_options_carry_a_retained_offline_last_will_on_the_status_topic()
    {
        var client = MqttNetPublisher.BuildClientOptions(Options());

        Assert.Multiple(
            () => Assert.Equal("njord/status", client.WillTopic),
            () => Assert.Equal("offline", Encoding.UTF8.GetString(client.WillPayload)),
            () => Assert.True(client.WillRetain),
            () => Assert.Equal(MqttQualityOfServiceLevel.AtLeastOnce, client.WillQualityOfServiceLevel));
    }

    [Fact]
    public void Client_options_carry_credentials_when_a_username_is_configured()
    {
        var client = MqttNetPublisher.BuildClientOptions(Options(o =>
        {
            o.Username = "njord";
            o.Password = "secret";
        }));

        var credentials = Assert.IsAssignableFrom<IMqttClientCredentialsProvider>(client.Credentials);
        Assert.Multiple(
            () => Assert.Equal("njord", credentials.GetUserName(client)),
            () => Assert.Equal("secret", Encoding.UTF8.GetString(credentials.GetPassword(client))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Client_options_have_no_credentials_without_a_username(string? username)
    {
        var client = MqttNetPublisher.BuildClientOptions(Options(o =>
        {
            o.Username = username;
            o.Password = "ignored";
        }));

        Assert.Null(client.Credentials);
    }
}
