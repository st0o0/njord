using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class ProjectionEngineSpec
{
    private static readonly DateTimeOffset Start = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(Start);

    private sealed record Seen(LocationForecast Current, LocationForecast? Previous);

    private sealed record ProbeEvent(Location Location, int Call) : EnrichmentEvent(Location);

    private sealed class RecordingGroup(params DomainKind[] required) : IProjectionGroup
    {
        public List<Seen> Calls { get; } = [];

        public bool EmitUnmappable { get; init; }

        public IReadOnlySet<DomainKind> RequiredDomains { get; } = new HashSet<DomainKind>(required);

        public bool AffectedBy(DomainKind domain) => RequiredDomains.Contains(domain);

        public IReadOnlyList<EnrichmentEvent> Evaluate(LocationForecast current, LocationForecast? previous, TimeProvider time)
        {
            Calls.Add(new Seen(current, previous));
            return EmitUnmappable ? [new ProbeEvent(current.Location, Calls.Count)] : [];
        }
    }

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
        var group = new RecordingGroup(DomainKind.Weather);
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
        var group = new RecordingGroup(DomainKind.Weather);
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
        var group = new RecordingGroup(DomainKind.Weather);
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
        var group = new RecordingGroup(DomainKind.Weather);
        var engine = new ProjectionEngine([group], Time);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(Weather(DomainData.Bern, 2.0));
        engine.Process(Weather(DomainData.Lucerne with { Name = "LUCERNE" }, 5.0));

        Assert.Multiple(
            () => Assert.Null(group.Calls[1].Previous),
            () => Assert.Equal(10.0, TemperatureOf(group.Calls[2].Previous?.Weather)));
    }

    [Fact]
    public void Only_groups_requiring_the_changed_domain_are_evaluated()
    {
        var weatherGroup = new RecordingGroup(DomainKind.Weather);
        var floodGroup = new RecordingGroup(DomainKind.Flood);
        var both = new RecordingGroup(DomainKind.Weather, DomainKind.Flood);
        var engine = new ProjectionEngine([weatherGroup, floodGroup, both], Time);

        engine.Process(Weather(DomainData.Lucerne, 10.0));
        engine.Process(DomainData.Received(DomainData.Lucerne, DomainData.Flood(Start)));

        Assert.Multiple(
            () => Assert.Equal(1, weatherGroup.Calls.Count),
            () => Assert.Equal(1, floodGroup.Calls.Count),
            () => Assert.Equal(2, both.Calls.Count));
    }

    [Fact]
    public void A_group_with_a_null_projection_produces_no_events()
    {
        var group = ProjectionGroupStub.NeverProjecting(DomainKind.Weather);
        var engine = new ProjectionEngine([group], Time);

        var events = engine.Process(Weather(DomainData.Lucerne, 10.0));

        Assert.IsType<PipelineEvent.WeatherDataReceived>(Assert.Single(events));
    }

    [Fact]
    public void Enrichment_events_follow_the_domain_event()
    {
        var engine = new ProjectionEngine([FeatureServices.AlertGroup(Time)], Time);

        var events = engine.Process(
            DomainData.Received(DomainData.Lucerne, DomainData.Weather(Start, 48, (_, p) => p with { Temperature = -6.0 })));

        Assert.IsType<PipelineEvent.WeatherDataReceived>(events[0]);
        var frost = Assert.Single(events.OfType<PipelineEvent.AlertRaised>(), e => e.Type == Messages.Alerts.AlertType.Frost);
        Assert.Multiple(
            () => Assert.Equal(Messages.Alerts.AlertSeverity.Orange, frost.Severity),
            () => Assert.Equal(Start, frost.Timestamp));
    }

    [Fact]
    public void An_enrichment_event_without_a_pipeline_mapping_is_rejected()
    {
        var engine = new ProjectionEngine([new RecordingGroup(DomainKind.Weather) { EmitUnmappable = true }], Time);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => engine.Process(Weather(DomainData.Lucerne, 10.0)));

        Assert.Equal("enrichmentEvent", ex.ParamName);
    }

    [Fact]
    public void Data_that_does_not_match_its_domain_is_rejected()
    {
        var engine = new ProjectionEngine([], Time);
        var mismatched = new DomainDataReceived(DomainData.Lucerne, DomainKind.Weather, DomainData.Flood(Start));

        var ex = Assert.Throws<ArgumentException>(() => engine.Process(mismatched));

        Assert.Equal("forecast", ex.ParamName);
    }

    private static class ProjectionGroupStub
    {
        private sealed record NoInput(Location Location) : IEnrichmentInput;

        private sealed class NullProjector(DomainKind domain) : IProjector<NoInput>
        {
            public IReadOnlySet<DomainKind> RequiredDomains { get; } = new HashSet<DomainKind> { domain };

            public NoInput? Project(LocationForecast current, LocationForecast? previous) => null;
        }

        public static IProjectionGroup NeverProjecting(DomainKind domain) =>
            new ProjectionGroup<NoInput>(new NullProjector(domain), []);
    }
}
