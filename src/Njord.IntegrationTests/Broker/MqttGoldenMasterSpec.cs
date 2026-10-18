using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Mqtt;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests.Broker;

// The retained state payloads of the deterministic analysis fixture, published through the real MQTTnet transport
// and read back by a second client, equal the golden masters of the in-process MQTT specs.
public sealed class MqttGoldenMasterSpec : IClassFixture<MosquittoFixture>
{
    private const string BaseTopic = "njord";

    private static readonly AnalysisFixture Fixture = AnalysisFixture.Create();

    private readonly MosquittoFixture _broker;

    public MqttGoldenMasterSpec(MosquittoFixture broker) => _broker = broker;

    public static TheoryData<string> Golden() => ["Alert", "Derived", "Trend", "Index", "Consensus"];

    private static IReadOnlyList<MqttMessage> Build(string kind) => [.. Fixture.Analyses.SelectMany(a => kind switch
    {
        "Alert" => StatePayloadBuilder.FromAlerts(a.Alerts, BaseTopic),
        "Derived" => StatePayloadBuilder.FromDerived(a.Derived, BaseTopic),
        "Trend" => StatePayloadBuilder.FromTrends(a.Trends, BaseTopic),
        "Index" => StatePayloadBuilder.FromIndices(a.Indices, BaseTopic),
        "Consensus" => StatePayloadBuilder.FromConsensus(a.Consensus, BaseTopic),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    })];

    private static Dictionary<string, string> ReadGolden(string kind)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", $"StatePayloadGoldenMasterSpec.{kind}_state_messages_match_golden_master.verified.txt");
        var lines = File.ReadAllLines(path).Select(l => l.TrimStart('﻿')).Where(l => l.Length > 0).ToList();
        Assert.Equal(0, lines.Count % 2);
        return Enumerable.Range(0, lines.Count / 2).ToDictionary(
            i => lines[2 * i].Replace(" retain=True", string.Empty, StringComparison.Ordinal),
            i => lines[(2 * i) + 1]);
    }

    [Theory(Timeout = TestTimeouts.Hosted)]
    [MemberData(nameof(Golden))]
    public async Task Retained_payloads_read_back_from_the_broker_equal_the_golden_master(string kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var golden = ReadGolden(kind);
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
                .Where(m => golden.ContainsKey(m.Topic))
                .GroupBy(m => m.Topic)
                .ToDictionary(g => g.Key, g => g.Last().Payload);
            Assert.Equal(golden.Count, received.Count);
            Assert.All(golden, entry => Assert.Equal(entry.Value, received[entry.Key]));
            Assert.All(observer.Messages.Where(m => golden.ContainsKey(m.Topic)), m => Assert.True(m.Retain, m.Topic));
            return Task.CompletedTask;
        }, ct);
    }
}
