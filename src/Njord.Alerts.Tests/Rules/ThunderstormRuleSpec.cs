using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class ThunderstormRuleSpec
{
    private static readonly ThunderstormRule Rule = new();

    private static readonly AlertSettings Settings = Defaults with
    {
        CapeThreshold = 1000,
        ThunderstormPrecipThreshold = 5,
        ThunderstormGustThreshold = 15,
    };

    [Fact]
    public void Thunderstorm_likely()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Cape, 1500.0), (Precipitation, 10.0), (WindGusts, 20.0)),
            MakeForecast(new WeatherModel("m2"), (Cape, 1200.0), (Precipitation, 8.0), (WindGusts, 18.0)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Thunderstorm_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Cape, 200.0), (Precipitation, 1.0), (WindGusts, 5.0)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
