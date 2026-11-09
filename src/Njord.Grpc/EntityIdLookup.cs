using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Results;

namespace Njord.Grpc;

public sealed class EntityIdLookup
{
    private static readonly string[] _suffixes = ["Result", "AlertResult", "ScoreResult", "ConditionResult", "TrendResult", "ForecastResult"];

    private static readonly Lazy<IReadOnlyList<Type>> _resultTypes = new(() =>
        typeof(FeatureResult).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false } && t.IsAssignableTo(typeof(FeatureResult)))
            .ToList());

    private readonly Dictionary<(Type Type, string Location), string> _ids = [];

    public EntityIdLookup(IEnumerable<IFeatureRule> rules, NjordOptions options)
    {
        var enabled = rules.Where(r => r.Enabled).Select(r => r.Descriptor).ToList();

        foreach (var location in options.ResolvedLocations)
        {
            var slug = ManifestBuilder.Slugify(location.Name);
            foreach (var descriptor in enabled)
            {
                if (descriptor.Output == OutputKind.Weather)
                {
                    _ids[(typeof(WeatherResult), location.Name)] = $"{slug}_weather";
                }
                else if (FindResultType(descriptor.Name) is { } type)
                {
                    _ids[(type, location.Name)] = $"{slug}_{ManifestBuilder.ToSnakeCase(descriptor.Name)}";
                }
            }
        }
    }

    public int Count => _ids.Count;

    public string? Find(FeatureResult result) =>
        _ids.GetValueOrDefault((result.GetType(), result.Location.Name));

    private static Type? FindResultType(string featureName)
    {
        foreach (var suffix in _suffixes)
        {
            var wanted = featureName + suffix;
            var match = _resultTypes.Value.FirstOrDefault(t => t.Name == wanted);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
