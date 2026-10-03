using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Njord.Tests.Shared;
using Xunit;

namespace Njord.Tests.Architecture;

public sealed class DisabledTestArchitectureSpec
{
    private static readonly string[] SkipProperties = ["Skip", "SkipUnless", "SkipWhen"];

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly[] TestAssemblies =
        [typeof(DisabledTestArchitectureSpec).Assembly, typeof(FakeOpenMeteoClient).Assembly];

    [Fact]
    public void Test_assemblies_contain_no_skipped_or_ignored_tests()
    {
        var offenders = TestAssemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(Disabled)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0, "Disabled tests found: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> Disabled(Type type)
    {
        if (type.CustomAttributes.Any(IsDisabling))
        {
            yield return type.FullName ?? type.Name;
        }

        foreach (var method in type.GetMethods(AllDeclared))
        {
            if (method.CustomAttributes.Any(IsDisabling))
            {
                yield return $"{type.FullName}.{method.Name}";
            }
        }
    }

    private static bool IsDisabling(CustomAttributeData attribute)
    {
        var attributeType = attribute.AttributeType;
        if (attributeType.Name is "Ignore" or "IgnoreAttribute")
        {
            return true;
        }

        return typeof(FactAttribute).IsAssignableFrom(attributeType)
            && attribute.NamedArguments.Any(a => SkipProperties.Contains(a.MemberName) && HasValue(a.TypedValue.Value));
    }

    private static bool HasValue(object? value)
        => value is not null && value is not string { Length: 0 };
}
