using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Marine;
using Njord.Features.Conditions.OceanCurrents;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.MarineFixtures;

namespace Njord.Features.Tests.Conditions.OceanCurrents;

public sealed class OceanCurrentsTests
{
    private static Features.Conditions.OceanCurrents.OceanCurrents Rule(double threshold = 3.0) =>
        new(Options.Create(new OceanCurrentsConfig { Enabled = true, StrongCurrentThresholdKmh = threshold }));

    [Fact]
    public void Strong_current_is_classified()
    {
        var point = new MarinePoint(Now, OceanCurrentVelocity: 4.0, OceanCurrentDirection: 180.0);
        var forecast = new LocationForecast(Lucerne, Marine: MarineForecast(point));

        var result = Run(Rule(), Now, forecast);

        var condition = Assert.IsType<OceanCurrentsResult>(result);
        Assert.Multiple(
            () => Assert.Equal(CurrentStrength.Strong, condition.Strength),
            () => Assert.Equal(4.0, condition.PeakVelocityKmh),
            () => Assert.Equal(180.0, condition.Direction));
    }

    [Fact]
    public void Weak_current_is_classified()
    {
        var point = new MarinePoint(Now, OceanCurrentVelocity: 0.5, OceanCurrentDirection: 90.0);
        var forecast = new LocationForecast(Lucerne, Marine: MarineForecast(point));

        var result = Run(Rule(), Now, forecast);

        var condition = Assert.IsType<OceanCurrentsResult>(result);
        Assert.Equal(CurrentStrength.Weak, condition.Strength);
    }

    [Fact]
    public void Very_strong_current_at_double_threshold()
    {
        var point = new MarinePoint(Now, OceanCurrentVelocity: 7.0, OceanCurrentDirection: 270.0);
        var forecast = new LocationForecast(Lucerne, Marine: MarineForecast(point));

        var result = Run(Rule(), Now, forecast);

        var condition = Assert.IsType<OceanCurrentsResult>(result);
        Assert.Equal(CurrentStrength.VeryStrong, condition.Strength);
    }

    [Fact]
    public void Returns_null_when_no_current_data()
    {
        var point = new MarinePoint(Now);
        var forecast = new LocationForecast(Lucerne, Marine: MarineForecast(point));

        var result = Run(Rule(), Now, forecast);

        Assert.Null(result);
    }
}
