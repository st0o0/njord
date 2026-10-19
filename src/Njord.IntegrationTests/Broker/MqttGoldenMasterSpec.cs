using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Mqtt;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Broker;

public sealed class MqttGoldenMasterSpec : IClassFixture<MosquittoFixture>
{
    private const string BaseTopic = "njord";

    private static readonly AnalysisFixture Fixture = AnalysisFixture.Create();

    private readonly MosquittoFixture _broker;

    public MqttGoldenMasterSpec(MosquittoFixture broker) => _broker = broker;

    public static TheoryData<string> Golden() => ["Alert"];

    private static IReadOnlyList<MqttMessage> Build(string kind) => [.. Fixture.Analyses.SelectMany(a => kind switch
    {
        "Alert" => StatePayloadBuilder.FromAlerts(a.Alerts, BaseTopic),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    })];

    [Theory(Timeout = TestTimeouts.Hosted)]
    [MemberData(nameof(Golden))]
    public async Task Retained_payloads_read_back_from_the_broker_equal_the_golden_master(string kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var expected = Build(kind).ToDictionary(m => m.Topic, m => m.Payload);
        var options = Options.Create(new MqttOptions { Host = "127.0.0.1", Port = _broker.Port, BaseTopic = BaseTopic });
        await using var publisher = new MqttNetPublisher(options, NullLogger<MqttNetPublisher>.Instance);
        await publisher.ConnectAsync((_, _) => { }, () => { }, ct);

        foreach (var message in Build(kind))
        {
            await publisher.SendAsync(message.Topic, message.Payload, message.Retain, ct);
        }

        await using var observer = await MqttObserver.SubscribeAsync(_broker.Port, $"{BaseTopic}/#", ct);
        await Eventually.AssertAsync(() =>
        {
            var received = observer.Messages
                .Where(m => expected.ContainsKey(m.Topic))
                .GroupBy(m => m.Topic)
                .ToDictionary(g => g.Key, g => g.Last().Payload);
            Assert.Equal(expected.Count, received.Count);
            Assert.All(expected, entry => Assert.Equal(entry.Value, received[entry.Key]));
            Assert.All(observer.Messages.Where(m => expected.ContainsKey(m.Topic)), m => Assert.True(m.Retain, m.Topic));
            return Task.CompletedTask;
        }, ct);
    }
}
