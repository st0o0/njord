using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Contracts = Njord.Messages.Config;
using DomainSection = Njord.Messages.Config.EnrichmentSection;

namespace Njord.Grpc.Tests;

public sealed class ConfigResponseMappingsSpec
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
