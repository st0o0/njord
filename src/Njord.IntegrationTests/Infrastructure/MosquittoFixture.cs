using System.Collections.Concurrent;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MQTTnet;
using MQTTnet.Protocol;

namespace Njord.IntegrationTests.Infrastructure;

public sealed class MosquittoFixture : IAsyncLifetime
{
    private IContainer? _container;
    private IMqttClient? _subscriber;
    private readonly ConcurrentBag<MqttMessage> _messages = [];

    public int MqttPort { get; private set; }
    public IReadOnlyCollection<MqttMessage> Messages => _messages;

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithImage("eclipse-mosquitto:2")
            .WithPortBinding(1883, true)
            .WithCommand("mosquitto", "-c", "/dev/null", "-p", "1883", "-v")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1883))
            .Build();

        await _container.StartAsync();
        MqttPort = _container.GetMappedPublicPort(1883);

        var factory = new MqttClientFactory();
        _subscriber = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", MqttPort)
            .WithClientId("e2e-subscriber")
            .Build();
        await _subscriber.ConnectAsync(options);

        _subscriber.ApplicationMessageReceivedAsync += args =>
        {
            _messages.Add(new MqttMessage(
                args.ApplicationMessage.Topic,
                args.ApplicationMessage.ConvertPayloadToString(),
                args.ApplicationMessage.Retain));
            return Task.CompletedTask;
        };

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("#", MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _subscriber.SubscribeAsync(subscribeOptions);
    }

    public void ClearMessages() => _messages.Clear();

    public async ValueTask DisposeAsync()
    {
        if (_subscriber is { IsConnected: true })
        {
            await _subscriber.DisconnectAsync();
        }

        _subscriber?.Dispose();

        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}

public sealed record MqttMessage(string Topic, string Payload, bool Retain);
