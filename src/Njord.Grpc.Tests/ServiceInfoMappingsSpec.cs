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
            TestServiceInfo.Feature(FeatureCategory.Flood, "FloodRisk"),
            TestServiceInfo.Feature(FeatureCategory.Energy, "SolarYield"),
            TestServiceInfo.Feature(FeatureCategory.Lifestyle, "Bbq"),
        ]);

        Assert.Equal(["alerts", "Energy:SolarYield", "Flood:FloodRisk", "Lifestyle:Bbq"], info.ActiveEnrichments);
    }

    [Fact]
    public void Active_enrichments_omit_alerts_when_disabled()
    {
        var info = TestServiceInfo.Create(
            alerts: new AlertOptions { Enabled = false },
            enabledFeatures: [TestServiceInfo.Feature(FeatureCategory.Flood, "FloodRain")]);

        Assert.Equal(["Flood:FloodRain"], info.ActiveEnrichments);
    }
}
