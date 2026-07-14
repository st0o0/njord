using Njord.Telemetry;

namespace Njord.Tests.Telemetry;

public sealed class NjordTelemetrySpec
{
    [Fact(Timeout = 5000)]
    public void Activity_source_has_name_njord()
    {
        Assert.Equal("njord", NjordTelemetry.Source.Name);
    }

    [Fact(Timeout = 5000)]
    public void Meter_has_name_njord()
    {
        Assert.Equal("njord", NjordTelemetry.Meter.Name);
    }

    [Fact(Timeout = 5000)]
    public void All_instruments_are_non_null()
    {
        Assert.NotNull(NjordTelemetry.PollsTotal);
        Assert.NotNull(NjordTelemetry.FetchTotal);
        Assert.NotNull(NjordTelemetry.FetchFailures);
        Assert.NotNull(NjordTelemetry.MqttPublishes);
        Assert.NotNull(NjordTelemetry.DiscoveryPublishes);
        Assert.NotNull(NjordTelemetry.DataChanges);
        Assert.NotNull(NjordTelemetry.Reconnects);
        Assert.NotNull(NjordTelemetry.FetchDuration);
        Assert.NotNull(NjordTelemetry.MqttPublishDuration);
        Assert.NotNull(NjordTelemetry.MqttConnected);
    }

    [Fact(Timeout = 5000)]
    public void All_instrument_names_are_unique()
    {
        var names = new[]
        {
            NjordTelemetry.PollsTotal.Name,
            NjordTelemetry.FetchTotal.Name,
            NjordTelemetry.FetchFailures.Name,
            NjordTelemetry.MqttPublishes.Name,
            NjordTelemetry.DiscoveryPublishes.Name,
            NjordTelemetry.DataChanges.Name,
            NjordTelemetry.Reconnects.Name,
            NjordTelemetry.FetchDuration.Name,
            NjordTelemetry.MqttPublishDuration.Name,
            NjordTelemetry.MqttConnected.Name,
        };

        Assert.Equal(names.Length, names.Distinct().Count());
    }
}
