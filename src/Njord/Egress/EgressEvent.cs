using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Egress;

public abstract record EgressEvent
{
    public sealed record PerModelUpdate(
        string Location,
        WeatherModel Model,
        IReadOnlyDictionary<string, string> HorizonPayloads) : EgressEvent;

    public sealed record ConsensusUpdate(string Location, ConsensusResult Result) : EgressEvent;
    public sealed record AlertUpdate(string Location, AlertResult Result) : EgressEvent;
    public sealed record DerivedUpdate(string Location, DerivedResult Result) : EgressEvent;
    public sealed record TrendUpdate(string Location, TrendResult Result) : EgressEvent;
    public sealed record IndexUpdate(string Location, IndexResult Result) : EgressEvent;
    public sealed record EnergyUpdate(string Location, EnergyResult Result) : EgressEvent;
    public sealed record HistoryUpdate(string Location, HistoryResult Result) : EgressEvent;
}
