namespace Njord.Persistence.Events.Polling;

public sealed record ForecastChanged(int Hash, DateTimeOffset Utc);
