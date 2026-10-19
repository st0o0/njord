using System.Reflection;
using System.Runtime.CompilerServices;

namespace Njord.Architecture.Tests;

public sealed class MappingConventionArchitectureSpec
{
    // Njord.Core is referenced by the host and by feature libraries without InternalsVisibleTo, so its two
    // configuration mapping classes stay public.
    private static readonly string[] PublicMappingClasses =
    [
        "Njord.Configuration.ServiceInfoMappings",
    ];

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static IEnumerable<Type> ProductionTypes() =>
        NjordArchitecture.ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    private static List<Type> MappingClasses() =>
        [.. ProductionTypes().Where(t => t.Name.EndsWith("Mappings", StringComparison.Ordinal) && !t.IsNested)];

    [Fact]
    public void Mapping_classes_exist_in_the_production_assemblies()
    {
        Assert.NotEmpty(MappingClasses());
    }

    [Fact]
    public void Mapping_classes_are_static_and_internal_unless_allow_listed()
    {
        var offenders = MappingClasses()
            .Where(t => !(t.IsAbstract && t.IsSealed) || (t.IsPublic != PublicMappingClasses.Contains(t.FullName)))
            .Select(t => t.FullName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_public_mapping_allow_list_names_existing_public_classes()
    {
        var existing = MappingClasses().Where(t => t.IsPublic).Select(t => t.FullName).Order(StringComparer.Ordinal);

        Assert.Equal(PublicMappingClasses.Order(StringComparer.Ordinal), existing);
    }

    [Fact]
    public void Mapping_classes_hold_no_instance_members_and_no_mutable_state()
    {
        var offenders = MappingClasses()
            .SelectMany(t => t.GetMembers(AllDeclared)
                .Where(m => m is not Type && m is not ConstructorInfo)
                .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .Where(IsInstanceOrMutable)
                .Select(m => $"{t.FullName}.{m.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Mapping_methods_are_not_named_Map()
    {
        var offenders = MappingClasses()
            .SelectMany(t => t.GetMethods(AllDeclared)
                .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal))
                .Select(m => $"{t.FullName}.{m.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsInstanceOrMutable(MemberInfo member) => member switch
    {
        FieldInfo field => !field.IsStatic || !(field.IsInitOnly || field.IsLiteral),
        MethodInfo method => !method.IsStatic,
        PropertyInfo property => !(property.GetMethod?.IsStatic ?? true) || property.SetMethod is not null,
        EventInfo => true,
        _ => false,
    };
}
