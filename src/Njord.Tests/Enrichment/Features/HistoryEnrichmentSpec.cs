using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment.Features;

public sealed class HistoryEnrichmentSpec
{
    private static readonly FakeTimeProvider Time = new(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero));

    private static HistoryEnrichment CreateFeature(bool enabled = true)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
            Enrichment = new EnrichmentOptions
            {
                History = new HistoryOptions { Enabled = enabled },
            },
        };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return new HistoryEnrichment(
            Options.Create(options), parameters, Time,
            new HistoryComputer(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HistoryEnrichment>.Instance);
    }

    [Fact]
    public void Implements_IActorEnrichment()
    {
        var feature = CreateFeature();

        Assert.IsAssignableFrom<IActorEnrichment>(feature);
    }

    [Fact]
    public void Enabled_reflects_options()
    {
        Assert.True(CreateFeature(enabled: true).Enabled);
        Assert.False(CreateFeature(enabled: false).Enabled);
    }

    [Fact]
    public void TypeName_is_history()
    {
        Assert.Equal("history", CreateFeature().TypeName);
    }
}
