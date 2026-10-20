using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Marine;
using Njord.Forecast.Solar;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Mqtt.Mapping;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class PipelineEventMqttMappingsSpec
{
    private static readonly DateTimeOffset Run = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Location Lucerne = DomainData.Lucerne;
    private static readonly MqttPublishContext Context = new("njord", Run);

    private static DomainForecast<T> Hourly<T>(DomainKind domain, T point) where T : IHasValidTime
        => new(domain, Run, TimeResolution.Hourly, [point]);

    private static string Payload(PipelineEvent pipelineEvent, string topic)
        => Assert.Single(pipelineEvent.ToMqttMessages(Context), m => m.Topic == topic).Payload;

    [Fact]
    public void A_weather_event_maps_to_the_weather_messages()
    {
        var forecast = DomainData.Weather(Run, hours: 2);

        var messages = new PipelineEvent.WeatherDataReceived(Lucerne, forecast).ToMqttMessages(Context);

        Assert.Equal(forecast.ToMessages(Lucerne, "njord"), messages);
    }

    [Fact]
    public void An_air_quality_event_maps_to_the_air_quality_topics()
    {
        var e = new PipelineEvent.AirQualityDataReceived(Lucerne,
            Hourly(DomainKind.AirQuality, new AirQualityPoint(Run, 8.5, 12, 60)));

        Assert.Equal("8.5", Payload(e, "njord/lucerne/air-quality/pm25"));
    }

    [Fact]
    public void A_flood_event_maps_to_the_flood_topics()
    {
        var e = new PipelineEvent.FloodDataReceived(Lucerne, DomainData.Flood(Run));

        Assert.Equal("12.5", Payload(e, "njord/lucerne/flood/discharge"));
    }

    [Fact]
    public void A_solar_event_maps_to_the_solar_topics()
    {
        var e = new PipelineEvent.SolarDataReceived(Lucerne, Hourly(DomainKind.Solar, new SolarPoint(Run, 1, 2, 3, 4)));

        Assert.Equal("3", Payload(e, "njord/lucerne/solar/shortwave_radiation"));
    }

    [Fact]
    public void A_marine_event_maps_to_the_marine_topics()
    {
        var e = new PipelineEvent.MarineDataReceived(Lucerne, Hourly(DomainKind.Marine, new MarinePoint(Run, 1.5, 6, 270)));

        Assert.Equal("1.5", Payload(e, "njord/lucerne/marine/wave_height"));
    }

    [Fact]
    public void An_alert_event_maps_to_one_retained_alert_message()
    {
        var alert = UnifiedAlert.Of(AlertType.FloodRisk, AlertSeverity.Red, 120, 100);

        var message = Assert.Single(new PipelineEvent.AlertRaised(Lucerne, alert, Run).ToMqttMessages(Context));

        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/alerts/flood_risk", message.Topic),
            () => Assert.True(message.Retain),
            () => Assert.Equal(
                "{\"severity\":\"red\",\"confidence\":1,\"trigger_value\":120,\"threshold\":100,"
                + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null}",
                message.Payload));
    }

    [Fact]
    public void A_lifestyle_event_maps_to_one_retained_lifestyle_message()
    {
        var message = Assert.Single(new PipelineEvent.LaundryScoreComputed(Lucerne, 80, Run, 4).ToMqttMessages(Context));

        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/lifestyle/laundry_score", message.Topic),
            () => Assert.True(message.Retain),
            () => Assert.Equal(
                "{\"score\":\"80\",\"best_start\":\"2026-01-15T12:00:00Z\",\"best_hours\":\"4\"}",
                message.Payload));
    }

    [Fact]
    public void An_unknown_event_maps_to_no_messages_and_the_unknown_location()
    {
        var unknown = new UnknownEvent();

        Assert.Multiple(
            () => Assert.Empty(unknown.ToMqttMessages(Context)),
            () => Assert.Equal("unknown", unknown.ToLocationName()));
    }

    [Fact]
    public void Known_events_report_their_location_name()
    {
        PipelineEvent[] events =
        [
            new PipelineEvent.FloodDataReceived(Lucerne, DomainData.Flood(Run)),
            new PipelineEvent.AlertRaised(Lucerne, Alert.None(AlertType.Pm25), Run),
            new PipelineEvent.CommuteSafety(Lucerne, "ok", []),
        ];

        Assert.All(events, e => Assert.Equal("Lucerne", e.ToLocationName()));
    }

    private sealed record UnknownEvent : PipelineEvent;
}
