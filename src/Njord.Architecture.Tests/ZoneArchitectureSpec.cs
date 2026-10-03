using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Njord.Architecture.Tests;

public sealed class ZoneArchitectureSpec
{
    [Fact]
    public void Enrichment_does_not_depend_on_mqtt_or_grpc()
    {
        Types().That().Are(NjordArchitecture.TypesInLibrary("Njord.Enrichment"))
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.TypesInLibrary("Njord.Mqtt"))
            .AndShould().NotDependOnAnyTypesThat().Are(NjordArchitecture.TypesInLibrary("Njord.Grpc"))
            .Check(NjordArchitecture.Instance);
    }

    [Fact]
    public void Mqtt_does_not_depend_on_enrichment()
    {
        Types().That().Are(NjordArchitecture.TypesInLibrary("Njord.Mqtt"))
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.TypesInLibrary("Njord.Enrichment"))
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
