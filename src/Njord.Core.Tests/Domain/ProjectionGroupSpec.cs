using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Core.Tests.Domain;

public sealed class ProjectionGroupSpec
{
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);

    private sealed record TestInput(Location Location, int Value) : IEnrichmentInput;

    private sealed record TestEvent(Location Location, string Source, int Value) : EnrichmentEvent(Location);

    private sealed class TestProjector(bool project) : IProjector<TestInput>
    {
        public int Calls { get; private set; }

        public IReadOnlySet<DomainKind> RequiredDomains { get; } = new HashSet<DomainKind> { DomainKind.Weather };

        public TestInput? Project(LocationForecast current, LocationForecast? previous)
        {
            Calls++;
            return project ? new TestInput(current.Location, 7) : null;
        }
    }

    private sealed class TestEnrichment(string name) : IEnrichment<TestInput>
    {
        public int Calls { get; private set; }

        public IReadOnlyList<EnrichmentEvent> Evaluate(TestInput input, TimeProvider time)
        {
            Calls++;
            return [new TestEvent(input.Location, name, input.Value)];
        }
    }

    [Fact]
    public void Evaluate_projects_once_and_runs_every_enrichment_on_the_same_input()
    {
        var projector = new TestProjector(project: true);
        var frost = new TestEnrichment("frost");
        var storm = new TestEnrichment("storm");
        var group = new ProjectionGroup<TestInput>(projector, [frost, storm]);

        var events = group.Evaluate(new LocationForecast(Lucerne), null, new FakeTimeProvider());

        Assert.Equal(1, projector.Calls);
        Assert.Equal(2, events.Count);
        Assert.Multiple(
            () => Assert.Equal(new TestEvent(Lucerne, "frost", 7), events[0]),
            () => Assert.Equal(new TestEvent(Lucerne, "storm", 7), events[1]));
    }

    [Fact]
    public void Null_projection_skips_all_enrichments()
    {
        var projector = new TestProjector(project: false);
        var enrichment = new TestEnrichment("frost");
        var group = new ProjectionGroup<TestInput>(projector, [enrichment]);

        var events = group.Evaluate(new LocationForecast(Lucerne), null, new FakeTimeProvider());

        Assert.Multiple(
            () => Assert.Empty(events),
            () => Assert.Equal(1, projector.Calls),
            () => Assert.Equal(0, enrichment.Calls));
    }

    [Fact]
    public void Projector_declares_its_required_domains()
    {
        var projector = new TestProjector(project: true);

        Assert.Equal([DomainKind.Weather], projector.RequiredDomains);
    }
}
