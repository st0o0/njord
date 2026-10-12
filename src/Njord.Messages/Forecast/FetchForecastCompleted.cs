namespace Njord.Messages.Forecast;

public sealed record FetchForecastCompleted(string Location, string ModelId, int Hash) : FetchForecastResponse;
