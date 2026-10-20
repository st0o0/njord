using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class ProjectionEngineSpec
{
    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(Start);

    private static double TemperatureOf(DomainForecast<WeatherPoint>? forecast)
    {
        Assert.NotNull(forecast);
        return forecast.Points[0].Temperature;
    }

    private static DomainDataReceived Weather(Location location, double temperature) =>
        DomainData.Received(location, DomainData.Weather(Start, 4, (_, p) => p with { Temperature = temperature }));

    [Fact]
    public void The_first_data_of_a_location_is_published_and_has_no_previous()
    {
        var group = new RecordingHandler(DomainKind.Weather);
        var engine = new ProjectionEngine([group], Time);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        var received = Assert.IsType<PipelineEvent.WeatherDataReceived>(Assert.Single(events));
        var seen = Assert.Single(group.Calls);
        Assert.Multiple(
            () => Assert.Equal("Lucerne", received.Location.Name),
            () => Assert.NotNull(seen.Current.Weather),
            () => Assert.Null(seen.Previous));
    }

    [Fact]
    public void A_second_update_of_the_same_domain_moves_the_old_value_to_previous()
    {
        var group = new RecordingHandler(DomainKind.Weather);
        var engine = new ProjectionEngine([group], Time);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Lucerne, 4.0));

        var second = group.Calls[1];
        Assert.NotNull(second.Previous);
        Assert.NotNull(second.Previous.Weather);
        Assert.Multiple(
            () => Assert.Equal(2, group.Calls.Count),
            () => Assert.Equal(4.0, TemperatureOf(second.Current.Weather)),
            () => Assert.Equal(10.0, second.Previous.Weather.Points[0].Temperature));
    }

    [Fact]
    public void An_update_of_another_domain_keeps_the_weather_previous_slot()
    {
        var group = new RecordingHandler(DomainKind.Weather);
        var engine = new ProjectionEngine([group], Time);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Lucerne, 4.0));
        engine.Process(DomainData.Received(DomainData.Lucerne, DomainData.Flood(Start)));
        engine.Process(Weather(DomainData.Lucerne, 1.0));

        var last = group.Calls[^1];
        Assert.NotNull(last.Previous);
        Assert.NotNull(last.Previous.Weather);
        Assert.Multiple(
            () => Assert.Equal(1.0, TemperatureOf(last.Current.Weather)),
            () => Assert.Equal(4.0, last.Previous.Weather.Points[0].Temperature),
            () => Assert.NotNull(last.Current.Flood));
    }

    [Fact]
    public void Locations_are_tracked_separately_and_case_insensitively()
    {
        var group = new RecordingHandler(DomainKind.Weather);
        var engine = new ProjectionEngine([group], Time);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Bern, 2.0));
        engine.Process(Weather(DomainData.Lucerne with { Name = "LUCERNE" }, 5.0));

        Assert.Multiple(
            () => Assert.Null(group.Calls[1].Previous),
            () => Assert.Equal(10.0, TemperatureOf(group.Calls[2].Previous?.Weather)));
    }

    [Fact]
    public void Only_handlers_triggered_by_the_changed_domain_are_invoked()
    {
        var weatherGroup = new RecordingHandler(DomainKind.Weather);
        var floodGroup = new RecordingHandler(DomainKind.Flood);
        var both = new RecordingHandler(DomainKind.Weather, DomainKind.Flood);
        var engine = new ProjectionEngine([weatherGroup, floodGroup, both], Time);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(DomainData.Received(DomainData.Lucerne, DomainData.Flood(Start)));

        Assert.Multiple(
            () => Assert.Single(weatherGroup.Calls),
            () => Assert.Single(floodGroup.Calls),
            () => Assert.Equal(2, both.Calls.Count));
    }

    [Fact]
    public void Alert_events_follow_the_domain_event()
    {
        var engine = new ProjectionEngine([FeatureServices.AlertHandler(Time)], Time);

        var events = engine.Process(
            DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start, 48, (_, p) => p with { Temperature = -6.0 })));

        Assert.IsType<PipelineEvent.WeatherDataReceived>(events[0]);
        var frost = Assert.Single(events.OfType<PipelineEvent.AlertRaised>(), e => e.Type == Messages.Alerts.AlertType.Frost);
        Assert.Multiple(
            () => Assert.Equal(Messages.Alerts.AlertSeverity.Orange, frost.Severity),
            () => Assert.Equal(Start, frost.Timestamp));
    }

    [Fact]
    public void Events_of_a_triggered_handler_follow_the_domain_event()
    {
        var engine = new ProjectionEngine([new EmittingHandler()], Time);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.Equal(2, events.Count);
        Assert.Multiple(
            () => Assert.IsType<PipelineEvent.WeatherDataReceived>(events[0]),
            () => Assert.Equal(AlertType.Frost, Assert.IsType<PipelineEvent.AlertRaised>(events[1]).Type),
            () => Assert.Equal(Start, Assert.IsType<PipelineEvent.AlertRaised>(events[1]).Timestamp));
    }

    [Fact]
    public void Data_that_does_not_match_its_domain_is_rejected()
    {
        var engine = new ProjectionEngine([], Time);
        var mismatched = new DomainDataReceived(DomainData.Lucerne, DomainKind.Weather, DomainData.Flood(Start));

        var ex = Assert.Throws<ArgumentException>(() => engine.Process(mismatched));

        Assert.Equal("forecast", ex.ParamName);
    }

    [Fact]
    public void Evaluate_returns_the_new_state_without_changing_the_given_one()
    {
        var group = new RecordingHandler(DomainKind.Weather);
        var initial = ProjectionEngine.EmptyState;

        var (state, events) = ProjectionEngine.Evaluate(initial, [group], Weather(DomainData.Lucerne, 10.0), Time);

        var slot = state["lucerne"];
        Assert.Multiple(
            () => Assert.Empty(initial),
            () => Assert.Single(events),
            () => Assert.Equal(10.0, TemperatureOf(slot.Current.Weather)),
            () => Assert.Null(slot.Previous));
    }

    [Fact]
    public void Evaluate_keeps_the_replaced_value_as_previous()
    {
        var group = new RecordingHandler(DomainKind.Weather);
        var (first, _) = ProjectionEngine.Evaluate(ProjectionEngine.EmptyState, [group], Weather(DomainData.Lucerne, 10.0), Time);

        var (second, _) = ProjectionEngine.Evaluate(first, [group], Weather(DomainData.Lucerne, 12.0), Time);

        var slot = second["Lucerne"];
        Assert.NotNull(slot.Previous);
        Assert.Multiple(
            () => Assert.Equal(12.0, TemperatureOf(slot.Current.Weather)),
            () => Assert.Equal(10.0, TemperatureOf(slot.Previous.Weather)));
    }

    [Fact]
    public void Evaluate_only_invokes_handlers_triggered_by_the_domain()
    {
        var weather = new RecordingHandler(DomainKind.Weather);
        var flood = new RecordingHandler(DomainKind.Flood);

        ProjectionEngine.Evaluate(ProjectionEngine.EmptyState, [weather, flood], Weather(DomainData.Lucerne, 10.0), Time);

        Assert.Multiple(
            () => Assert.Single(weather.Calls),
            () => Assert.Empty(flood.Calls));
    }
}
