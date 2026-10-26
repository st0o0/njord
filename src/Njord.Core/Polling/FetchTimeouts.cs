namespace Njord.Core.Polling;

// Admission: how long the gateway may hold a request before it is fetched (queue + throttle).
public sealed record FetchTimeouts(TimeSpan Admission)
{
    public static readonly FetchTimeouts Default = new(TimeSpan.FromSeconds(60));
}
