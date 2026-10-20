using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.Grpc.Tests;

public sealed class ServiceInfoMappingsSpec
{
    [Fact]
    public void Active_enrichments_list_alerts_and_every_enabled_toggle()
    {
        var info = TestServiceInfo.Create(enabledFeatures:
        [
            TestServiceInfo.Feature(FeatureKind.FloodRisk),
            TestServiceInfo.Feature(FeatureKind.SolarYield),
            TestServiceInfo.Feature(FeatureKind.BBQScore),
        ]);

        Assert.Equal(["alerts", "BBQScore", "FloodRisk", "SolarYield"], info.ActiveEnrichments);
    }

    [Fact]
    public void Active_enrichments_omit_alerts_when_disabled()
    {
        var info = TestServiceInfo.Create(
            alerts: new AlertOptions { Enabled = false },
            enabledFeatures: [TestServiceInfo.Feature(FeatureKind.FloodRain)]);

        Assert.Equal(["FloodRain"], info.ActiveEnrichments);
    }
}
