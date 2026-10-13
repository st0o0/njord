using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class FogRuleSpec
{
    private static readonly FogRule Rule = new();

    [Fact]
    public void Fog_likely()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, 5.0), (DewpointParam, 4.5), (WindSpeed, 1.0), (HumidityParam, 95.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 5.0), (DewpointParam, 4.0), (WindSpeed, 2.0), (HumidityParam, 92.0)));

        var alert = Rule.Evaluate(consensus, Defaults with { FogPersistentHours = 25 });

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Fog_not_likely()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, 20.0), (DewpointParam, 10.0), (WindSpeed, 5.0), (HumidityParam, 60.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Fog_persistent_produces_orange()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, 5.0), (DewpointParam, 4.5), (WindSpeed, 1.0), (HumidityParam, 95.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 5.0), (DewpointParam, 4.0), (WindSpeed, 2.0), (HumidityParam, 92.0)));

        var alert = Rule.Evaluate(consensus, Defaults with { FogPersistentHours = 4 });

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }
}
