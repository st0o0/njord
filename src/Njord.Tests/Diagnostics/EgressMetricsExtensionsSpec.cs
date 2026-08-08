using System.Diagnostics.Metrics;
using Njord.Diagnostics;

namespace Njord.Tests.Diagnostics;

public sealed class EgressMetricsExtensionsSpec
{
    [Fact(Timeout = 5000)]
    public void AddMqttDedup_creates_counter()
    {
        var counter = NjordMetrics.Instance.AddMqttDedup();

        Assert.Equal("njord_mqtt_dedup_total", counter.Name);
        Assert.Equal("{message}", counter.Unit);
    }

    [Fact(Timeout = 5000)]
    public void AddMqttConnected_creates_gauge()
    {
        var gauge = NjordMetrics.Instance.AddMqttConnected();

        Assert.Equal("njord_mqtt_connected", gauge.Name);
        Assert.Equal("1", gauge.Unit);
    }

    [Fact(Timeout = 5000)]
    public void MqttDedup_records_with_decision_label()
    {
        var counter = NjordMetrics.Instance.AddMqttDedup();
        KeyValuePair<string, object?>[]? recordedTags = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument == counter) ml.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => recordedTags = tags.ToArray());
        listener.Start();

        counter.Add(1,
            new KeyValuePair<string, object?>("location", "lucerne"),
            new KeyValuePair<string, object?>("decision", "published"));

        Assert.NotNull(recordedTags);
        Assert.Contains(recordedTags, t => t is { Key: "decision", Value: "published" });
    }
}
