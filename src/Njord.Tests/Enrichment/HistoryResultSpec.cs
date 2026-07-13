using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class HistoryResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather"], [], []);

    [Fact(Timeout = 5000)]
    public void Compute_with_empty_history_produces_null_metrics()
    {
        var history = new ForecastHistory(30);
        var snapshot = ModelSnapshot.Empty;

        var result = HistoryResult.Compute(history, snapshot, "lucerne", Parameters, Time, new HistoryOptions());

        Assert.Equal("lucerne", result.Location);
        Assert.Empty(result.Mae7d);
        Assert.Null(result.SeasonalBest);
    }

    [Fact(Timeout = 5000)]
    public void ToMqttMessages_produces_single_history_topic()
    {
        var history = new ForecastHistory(30);
        var snapshot = ModelSnapshot.Empty;
        var result = HistoryResult.Compute(history, snapshot, "lucerne", Parameters, Time, new HistoryOptions());
        var messages = result.ToMqttMessages("njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/history", messages[0].Topic);
        Assert.True(messages[0].Retain);

        var json = JsonNode.Parse(messages[0].Payload)!;
        Assert.True(json.AsObject().ContainsKey("seasonal_best"));
        Assert.True(json.AsObject().ContainsKey("anomaly"));
        Assert.True(json.AsObject().ContainsKey("weighted_temperature"));
    }
}
