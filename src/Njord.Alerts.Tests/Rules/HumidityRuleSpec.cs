using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class HumidityRuleSpec
{
    private static readonly HumidityRule Rule = new();

    [Fact]
    public void Humidity_muggy_produces_yellow()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (DewpointParam, 18.0), (IsDayParam, 1.0)),
            MakeForecast(new WeatherModel("m2"), (DewpointParam, 17.0), (IsDayParam, 1.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(HumidityComfort.Muggy, Assert.IsType<HumidityDetails>(alert.Details).ComfortLevel);
    }

    [Fact]
    public void Humidity_oppressive_produces_orange()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (DewpointParam, 22.0), (IsDayParam, 1.0)),
            MakeForecast(new WeatherModel("m2"), (DewpointParam, 23.0), (IsDayParam, 1.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(HumidityComfort.Oppressive, Assert.IsType<HumidityDetails>(alert.Details).ComfortLevel);
    }

    [Fact]
    public void Humidity_tropical_produces_red()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (DewpointParam, 25.0), (IsDayParam, 1.0)),
            MakeForecast(new WeatherModel("m2"), (DewpointParam, 26.0), (IsDayParam, 1.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(HumidityComfort.Tropical, Assert.IsType<HumidityDetails>(alert.Details).ComfortLevel);
    }

    [Fact]
    public void Humidity_comfortable_produces_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (DewpointParam, 12.0), (IsDayParam, 1.0)),
            MakeForecast(new WeatherModel("m2"), (DewpointParam, 10.0), (IsDayParam, 1.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Humidity_all_nighttime_produces_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (DewpointParam, 25.0), (IsDayParam, 0.0)),
            MakeForecast(new WeatherModel("m2"), (DewpointParam, 26.0), (IsDayParam, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Humidity_hours_with_a_day_night_tie_do_not_count_as_daytime()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (DewpointParam, 18.0), (IsDayParam, 1.0)),
            MakeForecast(new WeatherModel("m2"), (DewpointParam, 18.0), (IsDayParam, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(Alert.None(AlertType.Humidity), alert);
    }
}
