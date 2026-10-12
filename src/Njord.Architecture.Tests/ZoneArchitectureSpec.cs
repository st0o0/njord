using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Njord.Architecture.Tests;

public sealed class ZoneArchitectureSpec
{
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

    [Fact]
    public void Pipeline_depends_on_no_non_analysis_feature_library_or_host()
    {
        Types().That().Are(NjordArchitecture.TypesInLibrary(NjordArchitecture.PipelineLibrary))
            .Should().NotDependOnAnyTypesThat().Are(NjordArchitecture.TypesInPipelineDisallowedLibrariesAndHost)
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
