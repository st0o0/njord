using System.Collections.Immutable;
using Njord.Core.Configuration;
using Njord.Forecast;

namespace Njord.Core.Domain;

public sealed class FeatureRequirementsOptions
{
    public DomainRequirements Requirements { get; set; }
    public IReadOnlyList<DomainKind> Domains { get; set; } = [];
    public ImmutableArray<string> WeatherModels { get; set; } = [];
    public string PrimaryWeatherModel => WeatherModels.IsDefaultOrEmpty
        ? WeatherOptions.DefaultModel : WeatherModels[0];
}
