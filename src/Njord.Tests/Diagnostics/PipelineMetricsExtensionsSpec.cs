using System.Diagnostics.Metrics;
using Njord.Diagnostics;

namespace Njord.Tests.Diagnostics;

public sealed class PipelineMetricsExtensionsSpec
{
    [Fact(Timeout = 5000)]
    public void AddPollCycleDuration_creates_histogram_with_correct_name()
    {
        var histogram = NjordMetrics.Instance.AddPollCycleDuration();

        Assert.Equal("njord_poll_cycle_duration_seconds", histogram.Name);
        Assert.Equal("s", histogram.Unit);
    }

    [Fact(Timeout = 5000)]
    public void AddPollCycleModels_creates_gauge()
    {
        var gauge = NjordMetrics.Instance.AddPollCycleModels();

        Assert.Equal("njord_poll_cycle_models", gauge.Name);
    }

    [Fact(Timeout = 5000)]
    public void AddDataChanged_creates_counter()
    {
        var counter = NjordMetrics.Instance.AddDataChanged();

        Assert.Equal("njord_data_changed_total", counter.Name);
        Assert.Equal("{change}", counter.Unit);
    }

    [Fact(Timeout = 5000)]
    public void DataChanged_records_with_location_and_model_labels()
    {
        var counter = NjordMetrics.Instance.AddDataChanged();
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
            new KeyValuePair<string, object?>("model", "icon_d2"));

        Assert.NotNull(recordedTags);
        Assert.Contains(recordedTags, t => t is { Key: "location", Value: "lucerne" });
        Assert.Contains(recordedTags, t => t is { Key: "model", Value: "icon_d2" });
    }
}
