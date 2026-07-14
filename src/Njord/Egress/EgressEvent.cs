using Njord.Domain.Weather;

namespace Njord.Egress;

public abstract record EgressEvent
{
    public sealed record PerModelUpdate(
        string Location,
        WeatherModel Model,
        IReadOnlyDictionary<string, string> HorizonPayloads) : EgressEvent;

    public sealed record EnrichmentUpdate(string Location, string TypeName, object Result) : EgressEvent;
}
