using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Tests.Architecture;

internal static class NjordArchitecture
{
    private static readonly Assembly _njordAssembly = typeof(Njord.Ingest.OpenMeteoClient).Assembly;
    private static readonly Assembly _testsAssembly = typeof(NjordArchitecture).Assembly;

    public static readonly ArchUnitNET.Domain.Architecture Instance =
        new ArchLoader()
            .LoadAssemblies(_njordAssembly, _testsAssembly)
            .Build();

    public static readonly IObjectProvider<IType> Ingest =
        Types().That().ResideInNamespaceMatching(@"^Njord\.Ingest(\..*)?$").As("Ingest");

    public static readonly IObjectProvider<IType> Domain =
        Types().That().ResideInNamespaceMatching(@"^Njord\.Domain(\..*)?$").As("Domain");

    public static readonly IObjectProvider<IType> EgressSide =
        Types().That().ResideInNamespaceMatching(@"^Njord\.(Egress|Mqtt|Grpc)(\..*)?$").As("Egress side");

    public static IObjectProvider<IType> ProductionTypes =>
        Types().That().ResideInAssembly(_njordAssembly).As("Njord types");

    public static IObjectProvider<IType> TestTypes =>
        Types().That().ResideInAssembly(_testsAssembly).As("Njord.Tests types");
}
