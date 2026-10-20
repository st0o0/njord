using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Marine.Tests.MarineFixtures;

namespace Njord.Marine.Tests;

public sealed class SwimmingConditionsHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    private static SwimmingConditionsHandler Handler(bool enabled = true) =>
        new(new SwimmingConditionsRule(), AlertRuleFixture.OptionsWith(enabled));

    [Fact]
    public void Dangerous_conditions_raise_a_swimming_alert()
    {
        var current = new LocationForecast(Lucerne, Marine: MarineForecast(Point(0, 2.4)));

        var raised = Assert.IsType<AlertResult>(Assert.Single(Handler().Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal("swimming", raised.WireKey),
            () => Assert.Equal(AlertSeverity.Red, raised.Alert.Severity),
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp));
    }

    [Fact]
    public void Safe_conditions_or_missing_marine_data_raise_nothing()
    {
        var calm = new LocationForecast(Lucerne, Marine: MarineForecast(Point(0)));

        Assert.Multiple(
            () => Assert.Empty(Handler().Handle(calm, null, Time)),
            () => Assert.Empty(Handler().Handle(new LocationForecast(Lucerne), null, Time)));
    }

    [Fact]
    public void Disabled_alerts_raise_nothing()
    {
        var rough = new LocationForecast(Lucerne, Marine: MarineForecast(Point(0, 2.4)));

        Assert.Empty(Handler(enabled: false).Handle(rough, null, Time));
    }

    [Fact]
    public void The_handler_requires_wave_height_and_water_temperature_from_marine_data_only()
    {
        Assert.Equal(
            new DomainRequirements(Marine: MarineParams.WaveHeight, OptionalMarine: MarineParams.WaterTemperature),
            Handler().Feature.Requirements);
    }
}
