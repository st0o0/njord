using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Njord.Tests.Architecture;

public sealed class ZoneArchitectureSpec
{
    // Loading the architecture (IL analysis of two assemblies) takes ~6-10 s once per run; the shared static
    // initializer is what the first test waits on, so the default 5 s timeout is too tight for these specs.
    private const int ArchitectureTimeoutMs = 60_000;

    [Fact(Timeout = ArchitectureTimeoutMs)]
    public void Ingest_does_not_depend_on_egress_side()
    {
        Types().That().Are(NjordArchitecture.Ingest)
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.EgressSide)
            .Check(NjordArchitecture.Instance);
    }

    [Fact(Timeout = ArchitectureTimeoutMs)]
    public void Egress_side_does_not_depend_on_ingest()
    {
        Types().That().Are(NjordArchitecture.EgressSide)
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.Ingest)
            .Check(NjordArchitecture.Instance);
    }

    [Fact(Timeout = ArchitectureTimeoutMs)]
    public void Domain_does_not_depend_on_ingest_or_egress_side()
    {
        Types().That().Are(NjordArchitecture.Domain)
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.Ingest)
            .AndShould().NotDependOnAnyTypesThat().Are(NjordArchitecture.EgressSide)
            .Check(NjordArchitecture.Instance);
    }
}
