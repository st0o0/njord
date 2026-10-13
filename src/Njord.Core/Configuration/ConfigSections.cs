using System.Reflection;

namespace Njord.Core.Configuration;

public static class ConfigSections
{
    public static string PathOf<T>() => PathOf(typeof(T));

    public static string PathOf(Type optionsType) =>
        optionsType.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } field
            ? (string)field.GetRawConstantValue()!
            : throw new ArgumentException($"Options type '{optionsType.Name}' declares no SectionName constant.", nameof(optionsType));
}
