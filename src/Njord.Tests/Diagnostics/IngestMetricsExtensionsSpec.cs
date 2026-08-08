using System.Diagnostics.Metrics;
using Njord.Diagnostics;

namespace Njord.Tests.Diagnostics;

public sealed class IngestMetricsExtensionsSpec
{
    [Fact(Timeout = 5000)]
    public void AddFetchTotal_creates_counter_with_correct_name_and_unit()
    {
        var counter = NjordMetrics.Instance.AddFetchTotal();

        Assert.Equal("njord_fetch_total", counter.Name);
        Assert.Equal("{request}", counter.Unit);
    }

    [Fact(Timeout = 5000)]
    public void AddFetchDuration_creates_histogram_with_correct_name_and_unit()
    {
        var histogram = NjordMetrics.Instance.AddFetchDuration();

        Assert.Equal("njord_fetch_duration_seconds", histogram.Name);
        Assert.Equal("s", histogram.Unit);
    }

    [Fact(Timeout = 5000)]
    public void FetchTotal_records_with_labels()
    {
        var counter = NjordMetrics.Instance.AddFetchTotal();
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
            new KeyValuePair<string, object?>("model", "icon_d2"),
            new KeyValuePair<string, object?>("outcome", "success"));

        Assert.NotNull(recordedTags);
        Assert.Contains(recordedTags, t => t is { Key: "location", Value: "lucerne" });
        Assert.Contains(recordedTags, t => t is { Key: "model", Value: "icon_d2" });
        Assert.Contains(recordedTags, t => t is { Key: "outcome", Value: "success" });
    }

    [Fact(Timeout = 5000)]
    public void FetchDuration_records_value()
    {
        var histogram = NjordMetrics.Instance.AddFetchDuration();
        double? recordedValue = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument == histogram) ml.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => recordedValue = value);
        listener.Start();

        histogram.Record(1.2,
            new KeyValuePair<string, object?>("location", "lucerne"),
            new KeyValuePair<string, object?>("model", "icon_d2"));

        Assert.Equal(1.2, recordedValue);
    }
}
