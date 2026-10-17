namespace Njord.Mqtt.Payloads;

public readonly record struct TrendKey(string Value)
{
    public string DirectionKey => $"trend_{Value}_dir";

    public string DeltaKey => $"trend_{Value}_delta";

    public static TrendKey FromApiName(string apiName)
        => new(apiName.Replace("_2m", "").Replace("_10m", ""));
}
