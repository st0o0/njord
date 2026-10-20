using System.Collections.Immutable;
using Njord.Core.Configuration;

namespace Njord.Core.Domain;

// The models actually requested: all configured models when ModelComparison is enabled, else the primary only.
public sealed record EffectiveWeatherModels(ImmutableArray<string> Ids)
{
    public const string ComparisonFeatureName = "ModelComparison";

    public string Primary => Ids.IsDefaultOrEmpty ? WeatherOptions.DefaultModel : Ids[0];

    public int Count => Ids.IsDefaultOrEmpty ? 1 : Ids.Length;

    public bool Contains(string id) => !Ids.IsDefaultOrEmpty && Ids.Contains(id);

    public static EffectiveWeatherModels Resolve(WeatherOptions options, bool comparisonEnabled) =>
        new([.. comparisonEnabled ? options.EffectiveModels : [options.PrimaryModel]]);

    public static EffectiveWeatherModels Default { get; } = new([WeatherOptions.DefaultModel]);

    public IReadOnlyList<string> Ignored(WeatherOptions options) =>
        [.. options.EffectiveModels.Except(Ids)];
}
