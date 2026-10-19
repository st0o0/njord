using System.Collections.Immutable;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Mapping;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class PipelineEventMqttMappingsSpec
{
    private const string BaseTopic = "njord";

    private static readonly DateTimeOffset Run = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly Location Lucerne = DomainData.Lucerne;

    private static readonly MqttPublishContext Context = new(
        BaseTopic, AlertScenario.Parameters, Run);

    private static readonly Alert FrostAlert = new(
        AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3),
        PeakValue: -3.5, HoursUntil: 4);

    private static readonly PipelineEvent[] DomainEvents =
    [
        new PipelineEvent.WeatherDataReceived(Lucerne, DomainData.Weather(Run, hours: 2)),
        new PipelineEvent.AirQualityDataReceived(Lucerne, new DomainForecast<AirQualityPoint>(
            DomainKind.AirQuality, Run, TimeResolution.Hourly, [new AirQualityPoint(Run, 8.5, 12, 60)])),
        new PipelineEvent.FloodDataReceived(Lucerne, DomainData.Flood(Run)),
        new PipelineEvent.SolarDataReceived(Lucerne, new DomainForecast<SolarPoint>(
            DomainKind.Solar, Run, TimeResolution.Hourly, [new SolarPoint(Run, 1, 2, 3, 4)])),
        new PipelineEvent.MarineDataReceived(Lucerne, new DomainForecast<MarinePoint>(
            DomainKind.Marine, Run, TimeResolution.Hourly, [new MarinePoint(Run, 1.5, 6, 270)])),
    ];

    [Fact]
    public void A_weather_event_maps_to_the_weather_messages()
    {
        var forecast = DomainData.Weather(Run, hours: 2);
        var pipelineEvent = new PipelineEvent.WeatherDataReceived(Lucerne, forecast);

        Assert.Equal(forecast.ToMessages(Lucerne, BaseTopic), pipelineEvent.ToMqttMessages(Context));
    }

    [Fact]
    public void An_air_quality_event_maps_to_the_air_quality_messages()
    {
        var message = Assert.Single(
            DomainEvents.OfType<PipelineEvent.AirQualityDataReceived>().SelectMany(e => e.ToMqttMessages(Context)),
            m => m.Topic == "njord/lucerne/air-quality/pm25");

        Assert.Equal("8.5", message.Payload);
    }

    [Fact]
    public void A_flood_event_maps_to_the_flood_messages()
    {
        var message = Assert.Single(
            DomainEvents.OfType<PipelineEvent.FloodDataReceived>().SelectMany(e => e.ToMqttMessages(Context)),
            m => m.Topic == "njord/lucerne/flood/discharge");

        Assert.Equal("12.5", message.Payload);
    }

    [Fact]
    public void A_solar_event_maps_to_the_solar_messages()
    {
        var message = Assert.Single(
            DomainEvents.OfType<PipelineEvent.SolarDataReceived>().SelectMany(e => e.ToMqttMessages(Context)),
            m => m.Topic == "njord/lucerne/solar/shortwave_radiation");

        Assert.Equal("3", message.Payload);
    }

    [Fact]
    public void A_marine_event_maps_to_the_marine_messages()
    {
        var message = Assert.Single(
            DomainEvents.OfType<PipelineEvent.MarineDataReceived>().SelectMany(e => e.ToMqttMessages(Context)),
            m => m.Topic == "njord/lucerne/marine/wave_height");

        Assert.Equal("1.5", message.Payload);
    }

    [Fact]
    public void An_alert_event_maps_to_the_alert_state_message()
    {
        var pipelineEvent = new PipelineEvent.AlertRaised(Lucerne, FrostAlert, Run);

        var message = Assert.Single(pipelineEvent.ToMqttMessages(Context));

        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/alerts/frost", message.Topic),
            () => Assert.True(message.Retain),
            () => Assert.Equal(
                "{\"severity\":\"yellow\",\"confidence\":0.75,\"trigger_value\":0,\"threshold\":0,"
                + "\"peak_value\":-3.5,\"hours_until\":4,\"duration_hours\":null,"
                + "\"expected_low\":-2.1,\"earliest_frost\":\"\\u002B4h\",\"models_agreeing\":3}",
                message.Payload));
    }

    [Fact]
    public void An_air_quality_alert_event_maps_to_the_alert_state_message()
    {
        var pipelineEvent = new PipelineEvent.AirQualityAlertRaised(
            Lucerne, new AirQualityAlert(AirQualityAlertType.Pollen, AlertSeverity.Orange, 85, 50, HoursUntil: 3), Run);

        var message = Assert.Single(pipelineEvent.ToMqttMessages(Context));

        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/alerts/pollen", message.Topic),
            () => Assert.True(message.Retain),
            () => Assert.Equal(
                "{\"severity\":\"orange\",\"confidence\":1,\"trigger_value\":85,\"threshold\":50,"
                + "\"peak_value\":null,\"hours_until\":3,\"duration_hours\":null}",
                message.Payload));
    }

    [Fact]
    public void A_flood_alert_event_maps_to_the_alert_state_message()
    {
        var pipelineEvent = new PipelineEvent.FloodAlertRaised(
            Lucerne, new FloodAlert(FloodAlertType.FloodRisk, AlertSeverity.Red, 120, 100), Run);

        var message = Assert.Single(pipelineEvent.ToMqttMessages(Context));

        Assert.Multiple(
            () => Assert.Equal("njord/lucerne/alerts/flood_risk", message.Topic),
            () => Assert.True(message.Retain),
            () => Assert.Equal(
                "{\"severity\":\"red\",\"confidence\":1,\"trigger_value\":120,\"threshold\":100,"
                + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null}",
                message.Payload));
    }

    [Fact]
    public void A_domain_event_without_points_maps_to_no_messages()
    {
        var empty = new PipelineEvent.FloodDataReceived(
            Lucerne, new DomainForecast<FloodPoint>(DomainKind.Flood, Run, TimeResolution.Daily, ImmutableArray<FloodPoint>.Empty));

        Assert.Empty(empty.ToMqttMessages(Context));
    }

    [Fact]
    public void Every_produced_event_type_reports_its_location_name()
    {
        PipelineEvent[] events =
        [
            .. DomainEvents,
            new PipelineEvent.AlertRaised(Lucerne, FrostAlert, Run),
            new PipelineEvent.AirQualityAlertRaised(Lucerne, AirQualityAlert.None(AirQualityAlertType.Pm25), Run),
            new PipelineEvent.FloodAlertRaised(Lucerne, FloodAlert.None(FloodAlertType.FloodRain), Run),
        ];

        Assert.Equal(8, events.Length);
        Assert.All(events, e => Assert.Equal("Lucerne", e.ToLocationName()));
    }

    [Fact]
    public void An_unknown_event_maps_to_no_messages_and_the_unknown_location()
    {
        var pipelineEvent = new UnknownEvent();

        Assert.Empty(pipelineEvent.ToMqttMessages(Context));
        Assert.Equal("unknown", pipelineEvent.ToLocationName());
    }

    private sealed record UnknownEvent : PipelineEvent;
}
