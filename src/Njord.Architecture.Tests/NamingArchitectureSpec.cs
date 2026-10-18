using System.Runtime.CompilerServices;
using Assembly = System.Reflection.Assembly;

namespace Njord.Architecture.Tests;

public sealed class NamingArchitectureSpec
{
    private static IEnumerable<(Assembly Assembly, Type Type)> ProductionTypes()
        => NjordArchitecture.ProductionAssemblies
            .Where(a => a.GetName().Name!.StartsWith("Njord", StringComparison.Ordinal))
            .SelectMany(a => SafeTypes(a).Select(t => (Assembly: a, Type: t)))
            .Where(x => !x.Type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                        && !x.Type.FullName!.Contains("Microsoft.CodeCoverage", StringComparison.Ordinal)
                        && !(x.Type.Namespace?.StartsWith("Njord.Grpc.V2", StringComparison.Ordinal) ?? false)
                        && !x.Type.Name.StartsWith('<'));

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    [Fact]
    public void No_production_type_name_contains_Snapshot()
    {
        var offenders = ProductionTypes()
            .Where(x => x.Type.Name.Contains("Snapshot", StringComparison.Ordinal))
            .Select(x => x.Type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

}
