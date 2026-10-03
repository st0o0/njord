using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Njord.Tests.Architecture;

public sealed class ZoneArchitectureSpec
{
    [Fact]
    public void Ingest_does_not_depend_on_egress_side()
    {
        Types().That().Are(NjordArchitecture.Ingest)
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.EgressSide)
            .Check(NjordArchitecture.Instance);
    }

    [Fact]
    public void Egress_side_does_not_depend_on_ingest()
    {
        Types().That().Are(NjordArchitecture.EgressSide)
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.Ingest)
            .Check(NjordArchitecture.Instance);
    }

    [Fact]
    public void Domain_does_not_depend_on_ingest_or_egress_side()
    {
        Types().That().Are(NjordArchitecture.Domain)
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.Ingest)
            .AndShould().NotDependOnAnyTypesThat().Are(NjordArchitecture.EgressSide)
            .Check(NjordArchitecture.Instance);
    }
}
