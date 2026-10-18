using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Grpc.Mapping;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using DomainAlert = Njord.Compute.Contracts.Alert;
using DomainAlertSeverity = Njord.Compute.Contracts.AlertSeverity;
using DomainAlertType = Njord.Compute.Contracts.AlertType;
using DomainHorizonConsensus = Njord.Compute.Consensus.HorizonConsensus;
using DomainHorizonDerived = Njord.Compute.Contracts.HorizonDerived;
using DomainParameterConsensus = Njord.Compute.Consensus.ParameterConsensus;
using DomainParameterTrend = Njord.Compute.Contracts.ParameterTrend;
using DomainScalarDerived = Njord.Compute.Contracts.ScalarDerived;

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
