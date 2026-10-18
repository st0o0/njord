using System.Reflection;
using System.Runtime.CompilerServices;

namespace Njord.Architecture.Tests;

public sealed class MappingConventionArchitectureSpec
{
    // Njord.Core is referenced by the host and by feature libraries without InternalsVisibleTo, so its two
    // configuration mapping classes stay public.
    private static readonly string[] PublicMappingClasses =
    [
        "Njord.Core.Configuration.EffectiveConfigMappings",
        "Njord.Core.Configuration.ConfigUpdateMappings",
    ];

    private static readonly string[] ContractNamespaces =
    [
        "Njord.Grpc.V2",
        "Njord.Messages",
        "Njord.Mqtt.Payloads",
        "Njord.Compute.Contracts",
    ];

    // Legitimate leftovers that match a mapping method name but are not contract-to-contract transformations:
    // MapNjordGrpc follows the ASP.NET Core "Map*" endpoint-registration idiom (MapGrpcService, MapGet).
    private static readonly HashSet<string> MappingMethodAllowList = new(StringComparer.Ordinal)
    {
        "Njord.Grpc.GrpcEndpointExtensions.MapNjordGrpc",
    };

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

    [Fact]
    public void Contract_builders_outside_mapping_classes_are_not_private_static_Map_methods()
    {
        var offenders = ProductionTypes()
            .Where(t => !t.Name.EndsWith("Mappings", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods(AllDeclared)
                .Where(m => m is { IsStatic: true, IsPrivate: true })
                .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal))
                .Where(m => ReturnsContractType(m.ReturnType))
                .Select(m => $"{t.FullName}.{m.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_convention_detects_a_private_static_Map_builder()
    {
        var method = typeof(OffendingMapper).GetMethod("MapThing", AllDeclared);

        Assert.NotNull(method);
        Assert.True(method is { IsStatic: true, IsPrivate: true } && ReturnsContractType(method.ReturnType));
    }

    [Fact]
    public void Actors_and_services_declare_no_mapping_methods()
    {
        var offenders = ProductionTypes()
            .Where(t => !IsMappingHolder(t))
            .SelectMany(t => t.GetMethods(AllDeclared)
                .Where(m => IsMappingMethodName(m.Name))
                .Select(m => $"{t.FullName}.{m.Name}"))
            .Where(name => !MappingMethodAllowList.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_mapping_method_allow_list_names_existing_methods()
    {
        var existing = ProductionTypes()
            .SelectMany(t => t.GetMethods(AllDeclared).Select(m => $"{t.FullName}.{m.Name}"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(MappingMethodAllowList, name => Assert.Contains(name, existing));
    }

    [Theory]
    [InlineData("MapThing", true)]
    [InlineData("MapToMqttMessages", true)]
    [InlineData("ParseNumberArray", true)]
    [InlineData("ParseMixedArray", true)]
    [InlineData("BuildUri", true)]
    [InlineData("BuildRequestUri", true)]
    [InlineData("ParseNumber", false)]
    [InlineData("BuildPayload", false)]
    [InlineData("ToRequestUri", false)]
    public void The_mapping_method_name_rule_matches_the_documented_patterns(string name, bool expected)
    {
        Assert.Equal(expected, IsMappingMethodName(name));
    }

    [Fact]
    public void The_convention_detects_a_mapping_method_in_a_service()
    {
        var offenders = typeof(OffendingService).GetMethods(AllDeclared)
            .Where(m => IsMappingMethodName(m.Name))
            .Select(m => m.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["BuildUri", "MapThing", "ParseNumberArray"], offenders);
    }

    private static bool IsMappingHolder(Type type) =>
        type.Name.EndsWith("Mappings", StringComparison.Ordinal) || type.Name.EndsWith("Check", StringComparison.Ordinal);

    private static bool IsMappingMethodName(string name) =>
        name.StartsWith("Map", StringComparison.Ordinal)
        || (name.StartsWith("Parse", StringComparison.Ordinal) && name.EndsWith("Array", StringComparison.Ordinal))
        || (name.StartsWith("Build", StringComparison.Ordinal) && name.EndsWith("Uri", StringComparison.Ordinal));

    private static bool IsInstanceOrMutable(MemberInfo member) => member switch
    {
        FieldInfo field => !field.IsStatic || !(field.IsInitOnly || field.IsLiteral),
        MethodInfo method => !method.IsStatic,
        PropertyInfo property => !(property.GetMethod?.IsStatic ?? true) || property.SetMethod is not null,
        EventInfo => true,
        _ => false,
    };

    private static bool ReturnsContractType(Type type)
    {
        var inspected = Nullable.GetUnderlyingType(type) ?? type;
        var ns = inspected.Namespace ?? string.Empty;
        return ContractNamespaces.Any(c => ns == c || ns.StartsWith(c + ".", StringComparison.Ordinal));
    }

    private sealed class OffendingService
    {
        public static string BuildUri() => "x";

        public static int MapThing() => 1;

        public static int[] ParseNumberArray() => [];
    }

    private static class OffendingMapper
    {
        private static Messages.Config.ConfigIssue MapThing() => new(null, "x");

        public static void Touch() => MapThing();
    }
}
