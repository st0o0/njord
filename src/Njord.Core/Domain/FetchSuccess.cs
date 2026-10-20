using Njord.Forecast;

namespace Njord.Core.Domain;

public sealed record FetchSuccess(IDomainForecast Forecast) : FetchResult;
