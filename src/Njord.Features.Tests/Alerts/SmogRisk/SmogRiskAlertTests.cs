using Microsoft.Extensions.Options;
using Njord.Features.Alerts.SmogRisk;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.SmogRisk;

public sealed class SmogRiskAlertTests
{
    private static SmogRiskAlert Rule() =>
        new(Options.Create(new SmogRiskConfig { Enabled = true }));

    [Fact]
    public void Red_severity_when_low_boundary_and_high_pm25()
    {
        var weather = Uniform(p => p with { BoundaryLayerHeight = 150.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 55.0 });
        var forecast = Combined(weather, air);

        var result = Assert.IsType<SmogRiskAlertResult>(Run(Rule(), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, result.Severity),
            () => Assert.Equal(55.0, result.Pm25),
            () => Assert.Equal(150.0, result.BoundaryLayerHeight));
    }

    [Fact]
    public void Yellow_severity_for_marginal_conditions()
    {
        var weather = Uniform(p => p with { BoundaryLayerHeight = 450.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 26.0 });
        var forecast = Combined(weather, air);

        var result = Assert.IsType<SmogRiskAlertResult>(Run(Rule(), Now, forecast));

        Assert.Equal(AlertSeverity.Yellow, result.Severity);
    }

    [Fact]
    public void Returns_null_when_boundary_layer_above_threshold()
    {
        var weather = Uniform(p => p with { BoundaryLayerHeight = 600.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 55.0 });
        var forecast = Combined(weather, air);

        Assert.Null(Run(Rule(), Now, forecast));
    }

    [Fact]
    public void Returns_null_when_pm25_below_threshold()
    {
        var weather = Uniform(p => p with { BoundaryLayerHeight = 150.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 10.0 });
        var forecast = Combined(weather, air);

        Assert.Null(Run(Rule(), Now, forecast));
    }
}
