using Njord.Weather;

namespace Njord.Messages.Forecast;

public sealed record FetchForecastCompleted(LocationId Location, string ModelId, int Hash) : FetchForecastResponse;
