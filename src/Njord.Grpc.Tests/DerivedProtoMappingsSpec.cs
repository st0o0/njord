using Njord.Consensus;
using Njord.Grpc.Mapping;
using Njord.Messages.Derived;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Model;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;
using DomainHorizonConsensus = Njord.Consensus.HorizonConsensus;
using DomainHorizonDerived = Njord.Messages.Derived.HorizonDerived;
using DomainParameterConsensus = Njord.Consensus.ParameterConsensus;
using DomainParameterTrend = Njord.Messages.Trends.ParameterTrend;
using DomainScalarDerived = Njord.Messages.Derived.ScalarDerived;

namespace Njord.Grpc.Tests;

public sealed class DerivedProtoMappingsSpec
{
    [Fact]
    public void Derived_should_map_horizon_entries_and_scalars()
    {
        var byHorizon = new Dictionary<Horizon, DomainHorizonDerived>
        {
            [Horizon.Hours(3)] = new DomainHorizonDerived(Beaufort: 4, WindChill: -2.5, DewPointComfort: DewPointComfort.Comfortable, Condition: WeatherCondition.PartlyCloudy),
        };
        var scalars = new DomainScalarDerived(DiurnalAmplitude: 12.3, SunshinePct: 65.0, Inversion: true);
        var result = new DerivedResult(new LocationId("lucerne"), byHorizon, scalars);

        var update = DerivedProtoMappings.ToProto(result);

        Assert.Single(update.ByHorizon);
        Assert.Equal("h3", update.ByHorizon[0].Horizon);
        Assert.Equal(4, update.ByHorizon[0].Beaufort);
        Assert.Equal(-2.5, update.ByHorizon[0].WindChill);
        Assert.Equal(Njord.Grpc.V2.DewPointComfort.Comfortable, update.ByHorizon[0].DewpointComfort);
        Assert.Equal("Partly cloudy", update.ByHorizon[0].WmoDescription);
        Assert.Equal(12.3, update.Scalars.DiurnalAmplitude);
        Assert.Equal(65.0, update.Scalars.SunshinePct);
        Assert.True(update.Scalars.Inversion);
    }
}
