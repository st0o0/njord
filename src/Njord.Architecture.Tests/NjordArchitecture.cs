using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Njord.Architecture.Tests;

internal static class NjordArchitecture
{
    private static readonly Assembly NjordAssembly = typeof(Njord.Configuration.NjordServiceSetup).Assembly;

    // Every test assembly copied next to this one by the project references (Njord.*Tests plus Shared, this one included).
    public static readonly Assembly[] TestAssemblies = Directory
        .GetFiles(AppContext.BaseDirectory, "Njord.*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(n => n!.EndsWith("Tests", StringComparison.Ordinal) || n == "Njord.Tests.Shared")
        .Order(StringComparer.Ordinal)
        .Select(n => Assembly.Load(n!))
        .ToArray();

    // Production libraries: every Njord.* assembly next to this one that is not a test assembly
    // (the host may reference a library only transitively, so its referenced assemblies are not enough).
    private static readonly Assembly[] LibraryAssemblies = Directory
        .GetFiles(AppContext.BaseDirectory, "Njord.*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(n => !n!.StartsWith("Njord.Tests", StringComparison.Ordinal) && !n.EndsWith("Tests", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .Select(n => Assembly.Load(n!))
        .ToArray();

    private static readonly string[] FeatureLibraryNames = ["Njord.Pipeline", "Njord.Egress", "Njord.Grpc", "Njord.Ingest", "Njord.Sensors", "Njord.Mqtt", "Njord.Enrichment"];
    private static readonly string[] BaseLibraryNames = ["Njord.Core", "Njord.Messages", "Njord.Persistence", "Njord.Domain"];

    public static readonly ArchUnitNET.Domain.Architecture Instance =
        new ArchLoader()
            .LoadAssemblies([NjordAssembly, .. LibraryAssemblies, .. TestAssemblies])
            .Build();

    public static IEnumerable<string> FeatureLibraries => FeatureLibraryNames;

    public static IEnumerable<string> BaseLibraries => BaseLibraryNames;

    public static IObjectProvider<IType> TypesInLibrary(string name)
        => Types().That().ResideInAssembly(AssemblyByName(name))
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As(name);

    public static IObjectProvider<IType> TypesInOtherLibrariesAndHost(string name)
        => Types().That().ResideInAssembly(NjordAssembly)
            .Or().ResideInAssembly(AssemblyByName(FeatureLibraryNames.First(n => n != name)),
                FeatureLibraryNames.Where(n => n != name).Skip(1).Select(AssemblyByName).ToArray())
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As("other feature libraries and the host");

    public static IObjectProvider<IType> FeatureLibrariesAndHost =>
        Types().That().ResideInAssembly(NjordAssembly,
                FeatureLibraryNames.Select(AssemblyByName).ToArray())
            .And().DoNotHaveFullNameContaining("Microsoft.CodeCoverage")
            .As("feature libraries and the host");

    private static Assembly AssemblyByName(string name)
        => LibraryAssemblies.Single(a => a.GetName().Name == name);

    public static IObjectProvider<IType> ProductionTypes =>
        Types().That().ResideInAssembly(NjordAssembly,
                LibraryAssemblies)
            .As("Njord types");

    public static IObjectProvider<IType> TestTypes =>
        Types().That().ResideInAssembly(TestAssemblies[0], TestAssemblies.Skip(1).ToArray()).As("Njord test types");
}
