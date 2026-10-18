using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

// The configuration binder appends configured items to a collection that already holds default values
// (e.g. Horizons = [3, 6, ...] plus a configured [6, 24]). A configured collection must replace its default,
// so after binding every configured list or array is rebound into a fresh instance.
public static class CollectionDefaults
{
    public static OptionsBuilder<T> ReplaceCollectionDefaults<T>(this OptionsBuilder<T> builder)
        where T : class =>
        builder.PostConfigure<IConfiguration>((options, configuration) =>
            Replace(options, configuration.GetSection(ConfigSections.PathOf<T>())));

    public static void Replace(object target, IConfiguration section)
    {
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var child = section.GetSection(property.Name);
            if (!child.GetChildren().Any())
            {
                continue;
            }

            var type = property.PropertyType;
            if (IsList(type))
            {
                property.SetValue(target, child.Get(type));
            }
            else if (type.IsClass && type != typeof(string) && !typeof(IDictionary).IsAssignableFrom(type)
                     && !IsGenericDictionary(type) && property.GetValue(target) is { } nested)
            {
                Replace(nested, child);
            }
        }
    }

    private static bool IsList(Type type) =>
        type.IsArray
        || (type.IsGenericType && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(IList<>) || definition == typeof(List<>)
                || definition == typeof(ICollection<>) || definition == typeof(IEnumerable<>)
                || definition == typeof(IReadOnlyList<>)));

    private static bool IsGenericDictionary(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() is var definition
        && (definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>)
            || definition == typeof(IReadOnlyDictionary<,>));
}
