using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class ServiceInfoMappingsSpec
{
    [Fact]
    public void Active_enrichments_list_alerts_and_every_enabled_toggle()
    {
        var enrichments = new EnrichmentOptions
        {
            WeatherAlerts = false,
            CommuteSafety = false,
            PollenAlert = false,
            Pm25Alert = false,
            FloodRisk = true,
            SolarYield = true,
            BBQScore = true,
        };

        var info = TestServiceInfo.Create(enrichments: enrichments);

        Assert.Equal(["alerts", "BBQScore", "FloodRisk", "SolarYield"], info.ActiveEnrichments);
    }

    [Fact]
    public void Active_enrichments_omit_alerts_when_disabled()
    {
        var info = TestServiceInfo.Create(
            alerts: new AlertOptions { Enabled = false },
            enrichments: new EnrichmentOptions { WeatherAlerts = false, CommuteSafety = false, PollenAlert = false, Pm25Alert = false, FloodRain = true });

        Assert.Equal(["FloodRain"], info.ActiveEnrichments);
    }
}
