namespace Njord.Grpc.Tests;

public sealed class ServiceInfoMappingsSpec
{
    [Fact]
    public void Active_enrichments_list_every_enabled_rule_sorted()
    {
        var info = TestServiceInfo.Create(rules:
        [
            new TestServiceInfo.FloodRisk(),
            new TestServiceInfo.SolarYield(),
            new TestServiceInfo.Bbq(),
        ]);

        Assert.Equal(["Energy:SolarYield", "Flood:FloodRisk", "Lifestyle:Bbq"], info.ActiveEnrichments);
    }

    [Fact]
    public void Active_enrichments_use_the_config_name_not_the_class_name()
    {
        var info = TestServiceInfo.Create(rules: [new TestServiceInfo.FrostAlert()]);

        Assert.Equal(["Weather:Frost"], info.ActiveEnrichments);
    }

    [Fact]
    public void Active_enrichments_omit_disabled_rules()
    {
        var info = TestServiceInfo.Create(rules: [new TestServiceInfo.FloodRisk(), new TestServiceInfo.FloodRain(enabled: false)]);

        Assert.Equal(["Flood:FloodRisk"], info.ActiveEnrichments);
    }
}
