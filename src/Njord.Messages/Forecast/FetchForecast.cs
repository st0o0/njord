using Njord.Forecast;
using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchForecast(LocationId Location, string ModelId);

public abstract record FetchForecastResponse;
