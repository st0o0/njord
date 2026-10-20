using System.Collections.Immutable;
using Njord.Forecast;

namespace Njord.Core.Domain;

public sealed record FetchSuccess(IDomainForecast Forecast) : FetchResult
{
    public ImmutableDictionary<string, IDomainForecast> Models { get; init; } = ImmutableDictionary<string, IDomainForecast>.Empty;
}
