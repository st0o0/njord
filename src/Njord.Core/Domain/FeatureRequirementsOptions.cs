using System.Collections.Immutable;
using Njord.Core.Configuration;
using Njord.Core.Forecast;

namespace Njord.Core.Domain;

public sealed class FeatureRequirementsOptions
{
    public DomainRequirements Requirements { get; set; }
    public IReadOnlyList<DomainKind> Domains { get; set; } = [];
    public ImmutableArray<string> WeatherModels { get; set; } = [];
    public string PrimaryWeatherModel => WeatherModels.IsDefaultOrEmpty
        ? ForecastOptions.DefaultModel : WeatherModels[0];
    public ImmutableArray<string> EnsembleModels { get; set; } = [];
    public EnsembleParams AllEnsemble => Requirements.AllEnsemble;
}
