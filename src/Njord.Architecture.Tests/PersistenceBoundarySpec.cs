using System.Reflection;
using Njord.Persistence.Events.Polling;

namespace Njord.Architecture.Tests;

public sealed class PersistenceBoundarySpec
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly PersistenceAssembly = typeof(PersistedPollWorkerState).Assembly;

    private static IEnumerable<Type> PersistenceTypes() =>
        PersistenceAssembly.GetTypes().Where(t => t.Namespace is not null && !t.Name.StartsWith('<'));

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
        {
            return Flatten(type.GetElementType()!);
        }

        return type.IsGenericType ? type.GetGenericArguments().SelectMany(Flatten).Append(type.GetGenericTypeDefinition()) : [type];
    }

    private static bool IsAllowed(Type type)
    {
        var ns = type.Namespace ?? string.Empty;
        return ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)
            || ns == "Njord.Persistence" || ns.StartsWith("Njord.Persistence.", StringComparison.Ordinal);
    }

    private static IEnumerable<string> ForeignReferences(Type type) =>
        type.GetMembers(Declared)
            .SelectMany(member => member switch
            {
                PropertyInfo p => Flatten(p.PropertyType),
                FieldInfo f => Flatten(f.FieldType),
                ConstructorInfo c => c.GetParameters().SelectMany(p => Flatten(p.ParameterType)),
                MethodInfo m => Flatten(m.ReturnType).Concat(m.GetParameters().SelectMany(p => Flatten(p.ParameterType))),
                _ => [],
            })
            .Concat(type.BaseType is { } baseType ? Flatten(baseType) : [])
            .Concat(type.GetInterfaces().SelectMany(Flatten))
            .Where(t => t != type && !IsAllowed(t))
            .Select(t => t.FullName ?? t.Name)
            .Distinct()
            .Select(name => $"{type.FullName} -> {name}");

    [Fact]
    public void Persistence_types_exist()
    {
        Assert.NotEmpty(PersistenceTypes());
    }

    [Fact]
    public void Persistence_types_reference_only_System_and_Njord_Persistence_types()
    {
        var offenders = PersistenceTypes()
            .SelectMany(ForeignReferences)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persistence_enums_are_named_Persisted()
    {
        var offenders = PersistenceTypes()
            .Where(t => t.IsEnum && !t.Name.StartsWith("Persisted", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persisted_enums_declare_explicit_values()
    {
        var offenders = PersistenceTypes()
            .Where(t => t.IsEnum)
            .Where(t => Enum.GetValues(t).Cast<object>().Select(Convert.ToInt64).Distinct().Count() != Enum.GetNames(t).Length)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_boundary_detects_a_domain_type_in_a_persisted_record()
    {
        var offenders = ForeignReferences(typeof(LeakyRecord)).ToList();

        var offender = Assert.Single(offenders);
        Assert.Equal($"{typeof(LeakyRecord).FullName} -> {typeof(Messages.Pipeline.PollPhase).FullName}", offender);
    }

    [Fact]
    public void The_boundary_detects_a_domain_type_in_a_generic_argument()
    {
        var offenders = ForeignReferences(typeof(LeakyCollection)).ToList();

        var offender = Assert.Single(offenders);
        Assert.Equal($"{typeof(LeakyCollection).FullName} -> {typeof(Messages.Pipeline.PollPhase).FullName}", offender);
    }

    [Fact]
    public void The_boundary_accepts_a_persisted_enum_and_system_types()
    {
        Assert.Empty(ForeignReferences(typeof(PersistedPollWorkerState)));
    }

    private sealed record LeakyRecord(Messages.Pipeline.PollPhase Phase, int Count);

    private sealed record LeakyCollection(IReadOnlyList<Messages.Pipeline.PollPhase> Phases);
}
