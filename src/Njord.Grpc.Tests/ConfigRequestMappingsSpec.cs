using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using DomainSection = Njord.Messages.Config.EnrichmentSection;

namespace Njord.Grpc.Tests;

public sealed class ConfigRequestMappingsSpec
{
    [Fact]
    public void Every_defined_proto_section_but_unspecified_maps_back_to_the_domain()
    {
        Assert.Multiple(
            () => Assert.Equal(DomainSection.Consensus, EnrichmentSection.Consensus.ToDomain()),
            () => Assert.Equal(DomainSection.Alerts, EnrichmentSection.Alerts.ToDomain()),
            () => Assert.Equal(DomainSection.Derived, EnrichmentSection.Derived.ToDomain()),
            () => Assert.Equal(DomainSection.Trends, EnrichmentSection.Trends.ToDomain()),
            () => Assert.Equal(DomainSection.Indices, EnrichmentSection.Indices.ToDomain()),
            () => Assert.Equal("section", Assert.Throws<ArgumentOutOfRangeException>(
                () => EnrichmentSection.Unspecified.ToDomain()).ParamName));
    }

    [Fact]
    public void A_set_enrichment_request_maps_reset_and_dry_run_to_the_command()
    {
        var request = new SetEnrichmentRequest
        {
            Reset = { EnrichmentSection.Alerts, EnrichmentSection.Indices },
            DryRun = true,
        };

        var command = request.ToCommand();

        Assert.Multiple(
            () => Assert.Equal([DomainSection.Alerts, DomainSection.Indices], command.Reset),
            () => Assert.True(command.DryRun),
            () => Assert.Null(command.Consensus));
    }
}
