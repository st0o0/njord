using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Core.Tests;

public sealed class EnrichmentKindSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AlertComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.AlertComputed(
            new AlertResult(new LocationId("loc"), []), At);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Alerts, enrichment.Kind);
    }

    [Fact]
    public void ToWireName_is_pinned_for_every_EnrichmentKind_member()
    {
        var names = Enum.GetValues<EnrichmentKind>().Select(k => k.ToWireName()).ToList();

        Assert.Equal(["alerts"], names);
    }

    [Fact]
    public void ToWireName_throws_for_an_undefined_value()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((EnrichmentKind)999).ToWireName());

        Assert.Equal("kind", ex.ParamName);
        Assert.Equal((EnrichmentKind)999, ex.ActualValue);
    }
}
