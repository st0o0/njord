using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Results;

namespace Njord.Grpc;

public sealed record EntityRegistration(
    string EntityId,
    string Location,
    FeatureDescriptor Descriptor);

public sealed class EntityRegistry
{
    private static readonly string[] _suffixes =
        ["Result", "AlertResult", "ScoreResult", "ConditionResult", "TrendResult", "ForecastResult"];

    private static readonly Lazy<IReadOnlyList<Type>> _resultTypes = new(() =>
        typeof(FeatureResult).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false } && t.IsAssignableTo(typeof(FeatureResult)))
            .ToList());

    private readonly Dictionary<(Type Type, string Location), string> _ids = [];
    private readonly Dictionary<(string FeatureName, string Location), string> _observationIds = [];

    public EntityRegistry(IEnumerable<IFeatureRule> rules, IOptions<NjordOptions> options,
        IOptions<ActiveModels> activeModels)
    {
        var list = new List<EntityRegistration>();
        var enabled = rules.Where(r => r.Enabled).ToList();
        var models = activeModels.Value;

        foreach (var locationName in options.Value.ResolvedLocations.Select(x => x.Name))
        {
            var slug = Slugify(locationName);

            foreach (var desc in enabled.Select(x => x.Descriptor))
            {
                if (desc.Output == OutputKind.Weather)
                {
                    var id = $"{slug}_weather";
                    list.Add(new EntityRegistration(id, locationName, desc));
                    _ids[(typeof(WeatherResult), locationName)] = id;

                    foreach (var model in models.ForecastModels.Skip(1))
                    {
                        var modelEntityId = $"{slug}_{Slugify(model)}_weather";
                        list.Add(new EntityRegistration(modelEntityId, locationName, desc));
                        _ids[(typeof(WeatherResult), $"{locationName}:{model}")] = modelEntityId;
                    }

                    foreach (var model in models.EnsembleModels)
                    {
                        var modelEntityId = $"{slug}_{Slugify(model)}_weather";
                        list.Add(new EntityRegistration(modelEntityId, locationName, desc));
                        _ids[(typeof(WeatherResult), $"{locationName}:{model}")] = modelEntityId;
                    }
                }
                else if (desc.Output == OutputKind.Observation)
                {
                    var featureSlug = ToSnakeCase(desc.Name);
                    var id = $"{slug}_{featureSlug}";
                    list.Add(new EntityRegistration(id, locationName, desc));
                    _observationIds[(desc.Name, locationName)] = id;
                }
                else
                {
                    var featureSlug = ToSnakeCase(desc.Name);
                    var id = $"{slug}_{featureSlug}";
                    list.Add(new EntityRegistration(id, locationName, desc));
                    if (FindResultType(desc.Name) is { } type)
                    {
                        _ids[(type, locationName)] = id;
                    }
                }
            }
        }

        All = list;
    }

    public IReadOnlyList<EntityRegistration> All { get; }

    public int Count => _ids.Count + _observationIds.Count;

    public string? Find(FeatureResult result) => result switch
    {
        ObservationResult obs => _observationIds.GetValueOrDefault((obs.FeatureName, obs.Location.Name)),
        WeatherResult { ModelId: { } modelId } w => _ids.GetValueOrDefault((typeof(WeatherResult),
            $"{w.Location.Name}:{modelId}")),
        _ => _ids.GetValueOrDefault((result.GetType(), result.Location.Name)),
    };

    internal static string Slugify(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(ascii.ToLower(CultureInfo.InvariantCulture).Trim(), @"[^a-z0-9]+", "_").Trim('_');
    }

    internal static string ToSnakeCase(string name) =>
        Regex.Replace(name, @"(?<!^)([A-Z])", "_$1").ToLower(CultureInfo.InvariantCulture);

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
