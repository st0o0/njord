using Njord.Forecast;

namespace Njord.Core.Domain;

public sealed record FetchPlan(
    IReadOnlySet<DomainKind> Domains,
    DomainRequirements Requirements,
    EffectiveWeatherModels Models,
    int WeatherCallWeight);
