using Microsoft.Extensions.Options;
using Njord.Features.Conditions.ElevatedWind;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.ElevatedWind;

public sealed class ElevatedWindTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    [Fact]
    public void Returns_result_when_80m_wind_present()
    {
        var rule = new Features.Conditions.ElevatedWind.ElevatedWind(Opts(new ElevatedWindConfig { Enabled = true }));
        var weather = Uniform(p => p with { WindSpeed80m = 12.5, WindDirection80m = 270, WindSpeed120m = 14.1, WindDirection120m = 265 });
        var result = Run(rule, Now, ForecastOnly(weather));
        var condition = Assert.IsType<ElevatedWindResult>(result);
        Assert.Equal(12.5, condition.Speed80m);
        Assert.Equal(270, condition.Direction80m);
        Assert.Equal(14.1, condition.Speed120m);
        Assert.Equal(265, condition.Direction120m);
    }

    [Fact]
    public void Returns_null_when_80m_data_missing()
    {
        var rule = new Features.Conditions.ElevatedWind.ElevatedWind(Opts(new ElevatedWindConfig { Enabled = true }));
        var weather = Uniform(p => p with { WindSpeed80m = null, WindDirection80m = null });
        var result = Run(rule, Now, ForecastOnly(weather));
        Assert.Null(result);
    }

    [Fact]
    public void Speed_120m_is_optional()
    {
        var rule = new Features.Conditions.ElevatedWind.ElevatedWind(Opts(new ElevatedWindConfig { Enabled = true }));
        var weather = Uniform(p => p with { WindSpeed80m = 10.0, WindDirection80m = 180, WindSpeed120m = null, WindDirection120m = null });
        var result = Run(rule, Now, ForecastOnly(weather));
        var condition = Assert.IsType<ElevatedWindResult>(result);
        Assert.Null(condition.Speed120m);
        Assert.Null(condition.Direction120m);
    }
}
