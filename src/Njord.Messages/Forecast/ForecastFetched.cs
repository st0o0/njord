namespace Njord.Messages.Forecast;

public sealed record ForecastFetched(string Location, string ModelId, int Hash);
