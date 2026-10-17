using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Contracts = Njord.Messages.Config;
using DomainSection = Njord.Messages.Config.EnrichmentSection;

namespace Njord.Grpc.Tests;

public sealed class ConfigProtoMappingsSpec
{
    [Theory]
    [InlineData(null, EnrichmentSection.Unspecified)]
    [InlineData(DomainSection.Consensus, EnrichmentSection.Consensus)]
    [InlineData(DomainSection.Alerts, EnrichmentSection.Alerts)]
    [InlineData(DomainSection.Derived, EnrichmentSection.Derived)]
    [InlineData(DomainSection.Trends, EnrichmentSection.Trends)]
    [InlineData(DomainSection.Indices, EnrichmentSection.Indices)]
    public void A_domain_section_maps_to_its_proto_member(DomainSection? section, EnrichmentSection expected) =>
        Assert.Equal(expected, section.ToProto());

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

    [Fact]
    public void A_rejected_analysis_response_fills_issues_and_the_joined_reason()
    {
        var response = new Contracts.UpdateAnalysisConfigRejected(
            [
                new Contracts.ConfigIssue(DomainSection.Alerts, "first"),
                new Contracts.ConfigIssue(null, "second"),
            ],
            DryRun: true).ToProto();

        Assert.Multiple(
            () => Assert.False(response.Applied),
            () => Assert.True(response.DryRun),
            () => Assert.Equal("first; second", response.RejectionReason),
            () => Assert.Equal(
                [
                    new ConfigIssue { Section = EnrichmentSection.Alerts, Message = "first" },
                    new ConfigIssue { Section = EnrichmentSection.Unspecified, Message = "second" },
                ],
                response.Issues));
    }
}
