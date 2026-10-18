using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Architecture.Tests;

internal static class NjordArchitecture
{
    private static readonly Assembly NjordAssembly = typeof(Configuration.CoreSetupContainer).Assembly;

    // Every test assembly copied next to this one by the project references (Njord.*Tests plus Shared, this one included).
    public static readonly Assembly[] TestAssemblies =
    [
        .. Directory
            .GetFiles(AppContext.BaseDirectory, "Njord.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n!.EndsWith("Tests", StringComparison.Ordinal) || n == "Njord.Tests.Shared")
            .Order(StringComparer.Ordinal)
            .Select(n => Assembly.Load(n!))
    ];

    // Production libraries: every Njord.* assembly next to this one that is not a test assembly
    // (the host may reference a library only transitively, so its referenced assemblies are not enough).
    private static readonly Assembly[] LibraryAssemblies =
    [
        .. Directory
            .GetFiles(AppContext.BaseDirectory, "Njord.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !n!.StartsWith("Njord.Tests", StringComparison.Ordinal) &&
                        !n.EndsWith("Tests", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n => Assembly.Load(n!))
    ];

    private static readonly string[] FeatureLibraryNames = ["Njord.Grpc", "Njord.OpenMeteo", "Njord.Budget", "Njord.SensorHub", "Njord.Mqtt", "Njord.Alerts", "Njord.Derived", "Njord.Trends", "Njord.Indices"];
    private static readonly string[] BaseLibraryNames = ["Njord.Consensus", "Njord.Core", "Njord.Messages", "Njord.Persistence", "Njord.Weather", "Njord.Sensors"];

    internal const string PipelineLibrary = "Njord.Pipeline";
    private static readonly string[] PipelineAllowedAnalysisLibs = ["Njord.Alerts", "Njord.Derived", "Njord.Trends", "Njord.Indices"];

    public static readonly ArchUnitNET.Domain.Architecture Instance =
        new ArchLoader()
            .LoadAssemblies([NjordAssembly, .. LibraryAssemblies, .. TestAssemblies])
            .Build();

    public static IEnumerable<string> FeatureLibraries => FeatureLibraryNames;

    public static IEnumerable<Assembly> ProductionAssemblies => [NjordAssembly, .. LibraryAssemblies];

    public static IEnumerable<string> BaseLibraries => BaseLibraryNames;

    public static IObjectProvider<IType> TypesInLibrary(string name)
        => Types().That().ResideInAssembly(AssemblyByName(name))
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As(name);

    public static IObjectProvider<IType> TypesInOtherLibrariesAndHost(string name)
        => Types().That().ResideInAssembly(NjordAssembly)
            .Or().ResideInAssembly(AssemblyByName(FeatureLibraryNames.First(n => n != name)),
                [.. FeatureLibraryNames.Where(n => n != name).Skip(1).Select(AssemblyByName)])
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As("other feature libraries and the host");

    public static IObjectProvider<IType> FeatureLibrariesAndHost =>
        Types().That().ResideInAssembly(NjordAssembly,
                [.. FeatureLibraryNames.Select(AssemblyByName)])
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As("feature libraries and the host");

    private static Assembly AssemblyByName(string name)
        => LibraryAssemblies.Single(a => a.GetName().Name == name);

    public static IObjectProvider<IType> TypesInPipelineDisallowedLibrariesAndHost
    {
        get
        {
            var disallowed = FeatureLibraryNames
                .Where(n => !PipelineAllowedAnalysisLibs.Contains(n))
                .ToArray();
            return Types().That().ResideInAssembly(NjordAssembly,
                    [.. disallowed.Select(AssemblyByName)])
                .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
                .As("non-analysis feature libraries and the host");
        }
    }

    private static readonly string[] AnalysisLibraryNames = ["Njord.Consensus", .. PipelineAllowedAnalysisLibs];

    public static IObjectProvider<IType> AnalysisTypes =>
        Types().That().ResideInAssembly(AssemblyByName(AnalysisLibraryNames[0]),
                [.. AnalysisLibraryNames.Skip(1).Select(AssemblyByName)])
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As("Consensus and the analysis feature libraries");

    public static IObjectProvider<IType> ProductionTypes =>
        Types().That().ResideInAssembly(NjordAssembly,
                LibraryAssemblies)
            .As("Njord types");

    public static IObjectProvider<IType> TestTypes =>
        Types().That().ResideInAssembly(TestAssemblies[0], [.. TestAssemblies.Skip(1)]).As("Njord test types");
}
