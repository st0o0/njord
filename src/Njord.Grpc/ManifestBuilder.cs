using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Results;
using ProtoEntityPlatform = Njord.Grpc.Proto.EntityPlatform;
using ProtoOutputKind = Njord.Grpc.Proto.OutputKind;

namespace Njord.Grpc;

public sealed class ManifestBuilder
{
    private readonly IReadOnlyList<Proto.EntityDefinition> _entities;

    public ManifestBuilder(
        IEnumerable<IFeatureRule> rules,
        NjordOptions options,
        ActiveModels activeModels)
    {
        var list = new List<Proto.EntityDefinition>();

        foreach (var location in options.ResolvedLocations)
        {
            var slug = Slugify(location.Name);

            foreach (var rule in rules)
            {
                if (!rule.Enabled)
                {
                    continue;
                }

                var desc = rule.Descriptor;

                if (desc.Output == OutputKind.Weather)
                {
                    list.Add(BuildDefinition(slug, location.Name, desc, $"{slug}_weather"));

                    foreach (var model in activeModels.ForecastModels.Skip(1))
                    {
                        var modelSlug = Slugify(model);
                        list.Add(BuildDefinition(slug, location.Name, desc, $"{slug}_{modelSlug}_weather"));
                    }

                    foreach (var model in activeModels.EnsembleModels)
                    {
                        var modelSlug = Slugify(model);
                        list.Add(BuildDefinition(slug, location.Name, desc, $"{slug}_{modelSlug}_weather"));
                    }
                }
                else
                {
                    var featureSlug = ToSnakeCase(desc.Name);
                    list.Add(BuildDefinition(slug, location.Name, desc, $"{slug}_{featureSlug}"));
                }
            }
        }

        _entities = list;
    }

    public IReadOnlyList<Proto.EntityDefinition> Entities => _entities;

    private static Proto.EntityDefinition BuildDefinition(
        string locationSlug,
        string locationName,
        FeatureDescriptor descriptor,
        string entityId)
    {
        var meta = descriptor.EntityMetadata;
        return new Proto.EntityDefinition
        {
            EntityId = entityId,
            Location = locationName,
            Feature = descriptor.Name,
            Platform = MapPlatform(meta?.Platform),
            DeviceClass = meta?.DeviceClass ?? "",
            Unit = meta?.Unit ?? "",
            Icon = meta?.Icon ?? "",
            Category = descriptor.Category.ToString().ToLowerInvariant(),
            OutputKind = MapOutputKind(descriptor.Output),
        };
    }

    private static ProtoEntityPlatform MapPlatform(EntityPlatform? platform) => platform switch
    {
        EntityPlatform.Weather => ProtoEntityPlatform.Weather,
        EntityPlatform.Sensor => ProtoEntityPlatform.Sensor,
        EntityPlatform.BinarySensor => ProtoEntityPlatform.BinarySensor,
        _ => ProtoEntityPlatform.Unspecified,
    };

    private static ProtoOutputKind MapOutputKind(OutputKind kind) => kind switch
    {
        OutputKind.Alert => ProtoOutputKind.Alert,
        OutputKind.Score => ProtoOutputKind.Score,
        OutputKind.Condition => ProtoOutputKind.Condition,
        OutputKind.Trend => ProtoOutputKind.Trend,
        OutputKind.Forecast => ProtoOutputKind.Forecast,
        OutputKind.Weather => ProtoOutputKind.Weather,
        OutputKind.Observation => ProtoOutputKind.Observation,
        _ => ProtoOutputKind.Unspecified,
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
}
