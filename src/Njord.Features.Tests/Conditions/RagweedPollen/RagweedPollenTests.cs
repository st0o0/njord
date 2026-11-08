using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.RagweedPollen;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.RagweedPollen;

public sealed class RagweedPollenTests
{
    private static Features.Conditions.RagweedPollen.RagweedPollen Rule() =>
        new(Options.Create(new RagweedPollenConfig { Enabled = true }));

    [Fact]
    public void Returns_peak_ragweed_value()
    {
        var aq = CleanAir(shape: (hour, p) => p with { RagweedPollen = hour == 3 ? 42.7 : 10.0 });
        var forecast = new LocationForecast(Lucerne, AirQuality: aq);

        var result = Assert.IsType<RagweedPollenResult>(Run(Rule(), Now, forecast));

        Assert.Equal(42.7, result.PeakValue);
    }

    [Fact]
    public void Returns_null_when_ragweed_data_missing()
    {
        var aq = CleanAir();
        var forecast = new LocationForecast(Lucerne, AirQuality: aq);

        Assert.Null(Run(Rule(), Now, forecast));
    }
}
