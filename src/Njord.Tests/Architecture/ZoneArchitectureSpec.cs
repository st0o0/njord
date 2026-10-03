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

    [Fact]
    public void Egress_does_not_depend_on_pipeline()
    {
        Types().That().ResideInNamespaceMatching(@"^Njord\.Egress(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Njord\.Pipeline(\..*)?$")
            .Check(NjordArchitecture.Instance);
    }

    public static TheoryData<string> FeatureLibraries => new(NjordArchitecture.FeatureLibraries);

    public static TheoryData<string> BaseLibraries => new(NjordArchitecture.BaseLibraries);

    [Theory]
    [MemberData(nameof(FeatureLibraries))]
    public void Feature_library_depends_on_no_other_feature_library_or_host(string library)
    {
        Types().That().Are(NjordArchitecture.TypesInLibrary(library))
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.TypesInOtherLibrariesAndHost(library))
            .Check(NjordArchitecture.Instance);
    }

    [Theory]
    [MemberData(nameof(BaseLibraries))]
    public void Base_library_depends_on_no_feature_library_or_host(string library)
    {
        Types().That().Are(NjordArchitecture.TypesInLibrary(library))
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.FeatureLibrariesAndHost)
            .Check(NjordArchitecture.Instance);
    }
}
