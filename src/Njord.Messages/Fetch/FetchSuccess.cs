using System.Collections.Immutable;
using Njord.Core.Forecast;

namespace Njord.Messages.Fetch;

public sealed record FetchSuccess(IDomainForecast Forecast) : FetchResult
{
    public ImmutableDictionary<string, IDomainForecast> Models { get; init; } = ImmutableDictionary<string, IDomainForecast>.Empty;
}
