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
        var weather = Uniform(p => p with { WindSpeed80M = 12.5, WindDirection80M = 270, WindSpeed120M = 14.1, WindDirection120M = 265 });
        var result = Run(rule, Now, ForecastOnly(weather));
        var condition = Assert.IsType<ElevatedWindResult>(result);
        Assert.Equal(12.5, condition.Speed80M);
        Assert.Equal(270, condition.Direction80M);
        Assert.Equal(14.1, condition.Speed120M);
        Assert.Equal(265, condition.Direction120M);
    }

    [Fact]
    public void Returns_null_when_80m_data_missing()
    {
        var rule = new Features.Conditions.ElevatedWind.ElevatedWind(Opts(new ElevatedWindConfig { Enabled = true }));
        var weather = Uniform(p => p with { WindSpeed80M = null, WindDirection80M = null });
        var result = Run(rule, Now, ForecastOnly(weather));
        Assert.Null(result);
    }

    [Fact]
    public void Speed_120m_is_optional()
    {
        var rule = new Features.Conditions.ElevatedWind.ElevatedWind(Opts(new ElevatedWindConfig { Enabled = true }));
        var weather = Uniform(p => p with { WindSpeed80M = 10.0, WindDirection80M = 180, WindSpeed120M = null, WindDirection120M = null });
        var result = Run(rule, Now, ForecastOnly(weather));
        var condition = Assert.IsType<ElevatedWindResult>(result);
        Assert.Null(condition.Speed120M);
        Assert.Null(condition.Direction120M);
    }
}
