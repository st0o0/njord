namespace Njord.Core.Configuration.Polling;

// Admission: how long the gateway may hold a request before it is fetched (queue + throttle).
// Http: the Open-Meteo client timeout. Reply: how long a requester waits for the gateway's answer;
// it must exceed Admission + Http, so a reply always arrives before the requester gives up.
public sealed record FetchTimeouts(TimeSpan Admission, TimeSpan Http, TimeSpan Reply)
{
    public static readonly FetchTimeouts Default =
        new(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(120));
}
