using System.Reflection;
using Akka.Actor;
using ArchUnitNET.xUnitV3;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Model;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Njord.Architecture.Tests;

public sealed class ZoneArchitectureSpec
{
    public static TheoryData<string> FeatureLibraries => [.. NjordArchitecture.FeatureLibraries];

    public static TheoryData<string> BaseLibraries => [.. NjordArchitecture.BaseLibraries];

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

    [Theory]
    [InlineData("Analyzer")]
    [InlineData("Rule")]
    [InlineData("Formulas")]
    public void Messages_declares_no_compute_logic(string suffix)
    {
        var offenders = typeof(Njord.Messages.Pipeline.QueryPollStates).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Select(t => t.FullName);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Grpc_contains_only_the_GrpcConsumer_actor()
    {
        var actors = typeof(Grpc.WeatherGrpcService).Assembly.GetTypes()
            .Where(t => typeof(ActorBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.FullName)
            .ToList();

        var allowed = Assert.Single(actors);
        Assert.Equal("Njord.Grpc.GrpcConsumer", allowed);
    }

    [Fact]
    public void Grpc_reads_no_options_and_calls_no_calculator_or_registry()
    {
        Types().That().Are(NjordArchitecture.TypesInLibrary("Njord.Grpc"))
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(@"^Microsoft\.Extensions\.Options\.IOptions")
            .AndShould().NotDependOnAny(
                typeof(ConfigView),
                typeof(BudgetCalculator))
            .Check(NjordArchitecture.Instance);
    }

    [Fact]
    public void No_actor_in_a_production_assembly_declares_a_public_nested_record()
    {
        var nested = NjordArchitecture.ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(ActorBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetNestedTypes(BindingFlags.Public))
            .Where(IsRecord)
            .Select(t => t.FullName);

        Assert.Empty(nested);
    }

    // Record classes and record structs both get a compiler-generated PrintMembers method.
    private static bool IsRecord(Type type) =>
        type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(System.Text.StringBuilder)]) is not null;
}
