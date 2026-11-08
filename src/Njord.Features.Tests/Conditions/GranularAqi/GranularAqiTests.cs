using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.GranularAqi;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.GranularAqi;

public sealed class GranularAqiTests
{
    private static Features.Conditions.GranularAqi.GranularAqi Rule() =>
        new(Options.Create(new GranularAqiConfig { Enabled = true }));

    [Fact]
    public void Returns_all_five_european_aqi_values()
    {
        var aq = CleanAir(shape: (_, p) => p with
        {
            EuropeanAqiPm25 = 30.0,
            EuropeanAqiPm10 = 20.0,
            EuropeanAqiNitrogenDioxide = 15.0,
            EuropeanAqiOzone = 50.0,
            EuropeanAqiSulphurDioxide = 10.0,
        });
        var forecast = new LocationForecast(Lucerne, AirQuality: aq);

        var result = Assert.IsType<GranularAqiResult>(Run(Rule(), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(30.0, result.Pm25Aqi),
            () => Assert.Equal(20.0, result.Pm10Aqi),
            () => Assert.Equal(15.0, result.No2Aqi),
            () => Assert.Equal(50.0, result.OzoneAqi),
            () => Assert.Equal(10.0, result.So2Aqi));
    }

    [Fact]
    public void Returns_null_when_no_air_quality_data()
    {
        var forecast = new LocationForecast(Lucerne);

        Assert.Null(Run(Rule(), Now, forecast));
    }
}
