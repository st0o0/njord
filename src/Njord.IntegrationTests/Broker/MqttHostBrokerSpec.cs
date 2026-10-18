using Njord.Grpc.V2;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Broker;

// The whole host with MQTT enabled against a real Mosquitto broker, reached through a severable TCP proxy.
[Collection(HostCollection.Name)]
public sealed class MqttHostBrokerSpec : IClassFixture<MosquittoFixture>, IAsyncLifetime
{
    private readonly MosquittoFixture _broker;
    private readonly TemporaryDirectory _directory = new();
    private readonly string _baseTopic = "njord-" + Guid.NewGuid().ToString("N")[..8];
    private TcpProxy? _proxy;
    private NjordTestHost? _host;

    public MqttHostBrokerSpec(MosquittoFixture broker) => _broker = broker;

    private string StatusTopic => $"{_baseTopic}/status";

    public async ValueTask InitializeAsync()
    {
        _proxy = new TcpProxy(() => _broker.Port);
        var proxy = _proxy;
        _host = await NjordTestHost.StartAsync(
            _directory.Path,
            settings =>
            {
                settings["Njord:Mqtt:Enabled"] = "true";
                settings["Njord:Mqtt:Host"] = "127.0.0.1";
                settings["Njord:Mqtt:Port"] = proxy.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
                settings["Njord:Mqtt:BaseTopic"] = _baseTopic;
            },
            TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        if (_proxy is not null)
        {
            await _proxy.DisposeAsync();
        }

        _directory.Dispose();
    }

    private static string? Latest(MqttObserver observer, string topic) =>
        observer.Messages.LastOrDefault(m => m.Topic == topic)?.Payload;

    private static bool HasState(MqttObserver observer, string baseTopic) =>
        observer.Messages.Any(m => m.Topic.StartsWith($"{baseTopic}/luzern/", StringComparison.Ordinal));

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_host_publishes_the_retained_online_status_and_state_topics()
    {
        var ct = TestContext.Current.CancellationToken;

        await Eventually.AssertAsync(async () =>
        {
            await using var late = await MqttObserver.SubscribeAsync(_broker.Port, $"{_baseTopic}/#", ct);
            await Eventually.AssertAsync(() =>
            {
                Assert.Equal("online", Latest(late, StatusTopic));
                Assert.True(HasState(late, _baseTopic), "No state topic was retained yet.");
                Assert.All(late.Messages, m => Assert.True(m.Retain, m.Topic));
                return Task.CompletedTask;
            }, ct, TimeSpan.FromSeconds(2));
        }, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_abrupt_connection_loss_publishes_the_last_will_and_the_host_comes_back_online()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.NotNull(_proxy);
        await using var observer = await MqttObserver.SubscribeAsync(_broker.Port, StatusTopic, ct);
        await Eventually.AssertAsync(() =>
        {
            Assert.Equal("online", Latest(observer, StatusTopic));
            return Task.CompletedTask;
        }, ct);

        _proxy.Sever();

        await Eventually.AssertAsync(() =>
        {
            Assert.Contains(observer.Messages, m => m.Payload == "offline");
            return Task.CompletedTask;
        }, ct);
        await Eventually.AssertAsync(() =>
        {
            Assert.Equal("online", Latest(observer, StatusTopic));
            return Task.CompletedTask;
        }, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted * 3)]
    public async Task After_a_broker_restart_the_host_reconnects_and_republishes_status_and_state()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var before = await MqttObserver.SubscribeAsync(_broker.Port, $"{_baseTopic}/#", ct))
        {
            await Eventually.AssertAsync(() =>
            {
                Assert.Equal("online", Latest(before, StatusTopic));
                Assert.True(HasState(before, _baseTopic));
                return Task.CompletedTask;
            }, ct);
        }

        await _broker.RestartAsync(ct);

        // The broker lost its retained messages; the next poll cycle (triggered here) publishes the state again.
        Assert.NotNull(_host);
        var ops = new OpsService.OpsServiceClient(_host.Channel);
        await Eventually.AssertAsync(async () =>
        {
            await ops.TriggerPollAsync(new TriggerPollRequest { Location = "luzern" }, cancellationToken: ct);
            await using var after = await MqttObserver.SubscribeAsync(_broker.Port, $"{_baseTopic}/#", ct);
            await Eventually.AssertAsync(() =>
            {
                Assert.Equal("online", Latest(after, StatusTopic));
                Assert.True(HasState(after, _baseTopic), "The state topics were not republished after the restart.");
                return Task.CompletedTask;
            }, ct, TimeSpan.FromSeconds(2));
        }, ct, TimeSpan.FromSeconds(75));
    }
}
