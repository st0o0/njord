using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public static class ApiVariableNames
{
    public static FrozenDictionary<TEnum, string> For<TEnum>() where TEnum : struct, Enum => Cache<TEnum>.Names;

    public static string ToApiName<TEnum>(this TEnum variable) where TEnum : struct, Enum => Cache<TEnum>.Names[variable];

    private static class Cache<TEnum> where TEnum : struct, Enum
    {
        public static readonly FrozenDictionary<TEnum, string> Names = Build();

        private static FrozenDictionary<TEnum, string> Build() =>
            typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToFrozenDictionary(
                    f => (TEnum)f.GetValue(null)!,
                    f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                        ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{f.Name} has no JsonStringEnumMemberName."));
    }
}
